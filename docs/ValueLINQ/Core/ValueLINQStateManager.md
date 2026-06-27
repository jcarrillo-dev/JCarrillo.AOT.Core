# Gestor de Estados: ValueLINQStateManager y Sincronización Core

`ValueLINQStateManager<T>` (ver [ValueLINQStateManager.cs](../../../JCarrillo.AOT.Core/ValueLINQ/ValueLINQStateManager.cs)) es el motor interno centralizado de ValueLINQ. Actúa como un gestor de estados estático y síncrono que administra un pool fijo de **4096 ranuras (slots)** de sesión activa por cada tipo de dato `T`, evitando la asignación dinámica de memoria y controlando el ciclo de vida de los buffers rentados del `ArrayPool<T>.Shared`.

---

## 1. Arquitectura Interna del Gestor

El gestor de estados se compone de cuatro pilares de ingeniería de bajo nivel:

### A. Token de 64 bits y Prevención de Torn Reads
Cada sesión activa se identifica unívocamente mediante un token `long` de 64 bits generado por el helper de bits [TokenHelper.cs](../../../JCarrillo.AOT.Core/ValueLINQ/TokenHelper.cs):
*   **Bits 0-11 (12 bits)**: Almacenan el `slotIndex` (índice físico de la ranura, de 0 a 4095). Esto permite un acceso directo $O(1)$ a la ranura en la tabla estática sin realizar búsquedas o hashings.
*   **Bits 12-63 (52 bits)**: Almacenan la `version` de la ranura (un contador secuencial que se incrementa cada vez que la ranura es reutilizada).

Para evitar **torn reads** (lecturas corruptas de 64 bits que ocurren cuando un hilo lee la mitad superior del token y otro escribe la mitad inferior en arquitecturas de 32 bits), el acceso al token está protegido con operaciones atómicas de hardware:
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static long LeerToken(ref long ubicacion)
    => Environment.Is64BitProcess 
        ? Volatile.Read(ref ubicacion) 
        : Interlocked.Read(ref ubicacion);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void EscribirToken(ref long ubicacion, long valor)
{
    if (Environment.Is64BitProcess)
        Volatile.Write(ref ubicacion, valor);
    else
        Interlocked.Exchange(ref ubicacion, valor);
}
```
Esto garantiza la consistencia del token a nivel de CPU sin penalizaciones de bloqueo de software.

### B. Asignador de Ranuras Determinista $O(1)$
El reciclaje de ranuras libres se gestiona mediante un stack estático pre-asignado (`_indicesLibresStack`) de 4096 enteros y un puntero de pila (`_topStack`).
*   **Reserva (Pop)**: Al inicializar una consulta, se extrae un índice libre de la pila en tiempo constante $O(1)$ bajo un bloqueo global muy rápido (`_stackRoot`).
*   **Devolución (Push)**: Al liberar la consulta, el índice se empuja de vuelta a la pila en $O(1)$.
Este diseño elimina la fragmentación de memoria y evita cualquier asignación de punteros o colecciones dinámicas en el Heap de GC.

### C. Modelo de Bloqueos Segmentados (Lock Striping)
Para prevenir la contención de hilos en aplicaciones altamente concurrentes, el StateManager implementa **Lock Striping de 1 a 1 por slot**.
En lugar de bloquear toda la tabla de estados durante operaciones de lectura/escritura, cada ranura posee su propio objeto de bloqueo dedicado (`_slotLocks[indice]`).
Cuando un hilo interactúa con una sesión (por ejemplo, añadiendo datos o redimensionando el buffer), únicamente adquiere el lock de esa ranura específica:
```csharp
lock (_slotLocks[indice])
{
    // Operación aislada en la ranura
}
```
Esto permite que hasta 4096 hilos operen de forma simultánea en diferentes consultas de forma completamente paralela y con cero contención de locks.

### D. Temporizador de Limpieza de Fondo (LimpiezaPeriodicTimer)
Para evitar fugas de memoria por consultas huérfanas que no invocaron a `Dispose()`, el gestor inicia un temporizador periódico asíncrono en segundo plano (`LimpiezaPeriodicTimer`) que despierta **cada 1 minuto (medido)**.
El limpiador recorre las ranuras y evalúa si una sesión activa no ha registrado accesos en un intervalo mayor a `TiempoLimpieza` (por defecto, **5 minutos (medido)**). Si expira, adquiere de forma segura el lock de la ranura, invalida el token, devuelve el buffer físico al `ArrayPool<T>` y retorna el índice al stack de libres.

---

## 2. Análisis Honesto de Población (Inserción)

La versión 1.1.0 introduce la población en bloque para mitigar la sobrecarga de sincronización de la población unitaria clásica. A continuación, se presenta el análisis comparativo cuantitativo real y consolidado.

### Datos Crudos de Benchmarks (Medidos)

*   **Entorno de Medición**: Windows 11, CPU AMD Ryzen 9 3950X (3.50GHz, 32 lógicos, 16 físicos), .NET SDK 10.0.301.
*   **Harness**: BenchmarkDotNet v0.15.8, compilación en modo Release.
*   **Muestra**: Tamaño de colección $N = 1000$.

| Método | Runtime | Latencia Media (Mean) (medido) | Memoria Asignada (medido) | Modo de Sincronización |
| :--- | :---: | :---: | :---: | :--- |
| `List_Int_Fixed` (Pre-allocated) | .NET 10.0 | 2,233.03 ns | 4,056 B | Ninguno (Sin Locks) |
| `List_Int_Block` (AddRange) | .NET 10.0 | 231.76 ns | 4,056 B | Ninguno (Sin Locks) |
| `ValueLINQStruct_Int_Fixed` (Unit) | .NET 10.0 | 32,838.03 ns | 0 B | $O(N)$ Locks (1000 lock/unlock) |
| `ValueLINQStruct_Int_Block` (Bulk) | .NET 10.0 | 178.29 ns | 0 B | **$O(1)$ Lock** (1 lock/unlock) |
| `ValueLINQRefStruct_Int_Fixed` (Unit) | .NET 10.0 | 32,901.43 ns | 0 B | $O(N)$ Locks (1000 lock/unlock) |
| `ValueLINQRefStruct_Int_Block` (Bulk) | .NET 10.0 | 171.30 ns | 0 B | **$O(1)$ Lock** (1 lock/unlock) |

> [!IMPORTANT]
> **Seguridad de Rendimiento por Diseño (Safety by Design)**:
> Esta disparidad crítica en el rendimiento (pasar de **171.30 ns (medido)** mediante copia en bloque a **32,901.43 ns (medido)** en inserción iterativa elemento a elemento) fundamenta la decisión de diseño de mantener los métodos de población (`Añadir(T)` y `Añadir(ReadOnlySpan<T>)`) con nivel de acceso **`internal`** en ambos structs de consulta.
> Si estas APIs fuesen públicas, el usuario podría caer con extrema facilidad en el antipatrón de poblar la sesión en un bucle interactivo clásico, induciendo una regresión de velocidad masiva de más de **190 veces (medido)** debido al coste fijo acumulado por la adquisición de locks síncronos de ranura en cada iteración ($O(N)$ locks). Al restringir las APIs al ámbito interno del framework, se obliga al programador a inicializar sus consultas a través de envolturas masivas seguras como el método de extensión `.ToValueQuery()`, garantizando que la transferencia de datos ocurra siempre bajo un costo atómico constante de un solo bloqueo ($O(1)$ lock) y a máxima velocidad de hardware.

---

## 3. Diagnóstico Técnico de la Población Unitaria vs Bloque

### El Coste de la Población Unitaria ($O(N)$ Locks)
En la población unitaria (`ValueLINQStruct_Int_Fixed`), el cliente ejecuta un bucle `for` llamando a `Añadir(i)` para cada elemento. Cada llamada individual a `Añadir` invoca a `AsegurarEspacio`, el cual adquiere un bloqueo `lock (_slotLocks[indice])`.
*   Para $N = 1000$, el procesador debe ejecutar **1000 adquisiciones y 1000 liberaciones de lock** de forma secuencial.
*   Incluso en ausencia de contención entre hilos, cada par lock/unlock consume tiempo en la CPU debido a las comprobaciones de exclusión mutua y barreras de memoria del runtime de .NET.
*   Esto genera una penalización sistemática de **~32,000 ns (medido)** dedicada exclusivamente a la sincronización de hilos, relegando la latencia útil de copia a un segundo plano y provocando que la operación sea 14.7 veces más lenta que una lista ordinaria.

### La Optimización de la Población en Bloque ($O(1)$ Lock)
La nueva población en bloque (`Añadir(ReadOnlySpan<T>)`) resuelve esta ineficiencia consolidando todo el trabajo en una sola operación atómica:
1.  **Sincronización Única ($O(1)$)**: Se calcula el tamaño final necesario ($Tama\tilde{n}oActual + Span.Length$) y se realiza **una única llamada** a `AsegurarEspacio`. Esto reduce el costo de locking de $O(N)$ a una única adquisición y liberación de lock ($O(1)$).
2.  **Copia Vectorial Directa**: El framework obtiene una vista directa del buffer de la sesión y realiza una copia en bloque utilizando `ReadOnlySpan<T>.CopyTo`. Esto se traduce en una transferencia directa de memoria física (`memcpy` altamente optimizado por hardware mediante instrucciones vectoriales de CPU).
3.  **Rendimiento en JIT .NET 10.0**: 
    -   Para `ValueLINQStruct<int>`, la latencia se reduce de 32,838.03 ns (medido) a **178.29 ns (medido)**, logrando **184.18x de aceleración (medido)** (−99.46% de tiempo de CPU).
    -   Para `ValueLINQRefStruct<int>`, la latencia baja de 32,901.43 ns (medido) a **171.30 ns (medido)**, logrando **192.07x de aceleración (medido)** (−99.48% de tiempo de CPU).
    -   **Comparativa vs Listas**: `ValueLINQRefStruct_Int_Block` es un **35.29% más rápido (medido)** que `List_Int_Block` (171.30 ns vs 231.76 ns), logrando además **0 B (medido)** asignados en heap frente a los **4,056 B (medido)** de la lista estándar.
4.  **Rendimiento en NativeAOT 10.0**:
    -   Para `ValueLINQStruct<int>`, la latencia bajo compilación nativa completa baja a **142.33 ns (medido)** frente a los 30,570.09 ns (medido) del método unitario, logrando **214.78x de aceleración (medido)** (−99.53% de tiempo de CPU).
    -   **Comparativa vs Listas**: `ValueLINQStruct_Int_Block` es un **6.23% más rápido (medido)** que `List_Int_Block` (142.33 ns vs 151.20 ns) con **0 B (medido)** de asignación en el Heap de Native AOT frente a los **4,056 B (medido)** de la lista estándar.
