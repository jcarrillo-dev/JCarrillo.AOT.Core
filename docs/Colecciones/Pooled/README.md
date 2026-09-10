[Volver al Módulo de Colecciones](../README.md)

# Colecciones Rentadas (Pooled Collections): Arquitectura y Gestión de Memoria


El espacio de nombres `JCarrillo.AOT.Core.Colecciones.Pooled` (ver [Colecciones/Pooled/](../../../JCarrillo.AOT.Core/Colecciones/Pooled/)) proporciona colecciones estructuradas optimizadas para escenarios de rendimiento crítico. Su objetivo primordial es eliminar la presión sobre el recolector de basura mediante el arrendamiento y reciclaje de buffers de memoria contigua desde el pool global de recursos (`ArrayPool<T>.Shared`).

---

## 1. Catálogo de Estructuras de Datos (v1.1.0)

El framework ofrece cuatro variantes de colecciones con diferentes compromisos de diseño entre flexibilidad en pila y seguridad estricta:

| Colección | Tipo de Struct | ¿Es Ampliable? | Características Técnicas |
| :--- | :--- | :---: | :--- |
| **`PooledList<T>`** | `record struct` | **SÍ** | Lista dinámica mutable, con validación de no-boxing activa en su `Dispose()`. El compilador permite guardarla en un campo de clase, pero hacerlo la copia y **corrompe el pool** (§3.4): para compartirla hay que aliasarla con `ref` o `in`. |
| **`PooledArray<T>`** | `record struct` | **NO** | Wrapper inmutable no-ampliable sobre un array del pool. Ofrece acceso por índice y vista de memoria (`Memory<T>`). |
| **`PooledListRef<T>`** | `ref struct` | **SÍ** | Variante de lista confinada estrictamente a la pila. Evita el boxing por diseño del compilador. No puede ser capturada en tareas asíncronas ni subirse al heap. |
| **`PooledArrayRef<T>`** | `ref struct` | **NO** | Variante de array confinado estrictamente a la pila. Su inmutabilidad y confinamiento se resuelven en tiempo de compilación. |

---

## 2. Métricas de Rendimiento Secuencial (Medidas)

Los benchmarks de las colecciones comparan la inicialización, inserción (`Add` / `AddRange`) e iteración síncrona frente a los baselines del sistema.

*   **Entorno de Medición**: Windows 11 (10.0.26200.8655), CPU AMD Ryzen 9 3950X, .NET SDK 10.0.301, runtime .NET 10.0.9 (medido).
*   **Harness**: BenchmarkDotNet v0.14.0, compilación en modo Release.

### Tabla 1: List\<int\> vs PooledList\<int\> (Medidos Multi-Runtime)
*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled.PooledListIntBenchmarks`.

| Runtime / Engine | Método de Prueba | Tamaño (N) | Latencia (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `ListIntFixed` | 100 | 267.3 ns | 456 B | 0.76 | Precapacidad BCL en AOT nativo. |
| **NativeAOT 10.0** | `PooledListIntFixed` | 100 | 270.6 ns | **0 B** | 0.77 | Búfer prealquilado del pool, cero alocaciones. |
| **NativeAOT 10.0** | `ListIntDynamic` (Baseline) | 100 | 352.6 ns | 1,184 B | 1.00 | Lista dinámica BCL con redimensionado en heap. |
| **NativeAOT 10.0** | `PooledListIntDynamic` | 100 | 353.6 ns | **0 B** | 1.00 | Redimensionado automático en pool sin GC. |
| **.NET 10.0 JIT** | `PooledListIntFixed` | 100 | **266.7 ns** | **0 B** | **0.71** | **29% más rápido que BCL dinámica**, 0 B en heap. |
| **.NET 10.0 JIT** | `PooledListIntDynamic` | 100 | **269.0 ns** | **0 B** | **0.71** | Cero impacto en GC en RyuJIT 10. |
| **.NET 10.0 JIT** | `ListIntFixed` | 100 | 298.3 ns | 456 B | 0.79 | Capacidad inicial fija BCL. |
| **.NET 10.0 JIT** | `ListIntDynamic` (Baseline) | 100 | 376.7 ns | 1,184 B | 1.00 | Lista estándar BCL dinámica. |
| **.NET 9.0 JIT** | `PooledListIntFixed` | 100 | 265.0 ns | **0 B** | 0.71 | Optimización en .NET 9. |
| **.NET 9.0 JIT** | `PooledListIntDynamic` | 100 | 272.5 ns | **0 B** | 0.73 | Cero asignación en .NET 9. |
| **.NET 9.0 JIT** | `ListIntFixed` | 100 | 279.1 ns | 456 B | 0.74 | Capacidad fija BCL en .NET 9. |
| **.NET 9.0 JIT** | `ListIntDynamic` (Baseline) | 100 | 375.5 ns | 1,184 B | 1.00 | Lista dinámica BCL en .NET 9. |
| **.NET 8.0 JIT** | `PooledListIntFixed` | 100 | 269.6 ns | **0 B** | 0.74 | Evaluación en .NET 8 LTS. |
| **.NET 8.0 JIT** | `PooledListIntDynamic` | 100 | 292.2 ns | **0 B** | 0.80 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `ListIntFixed` | 100 | 297.8 ns | 456 B | 0.81 | Capacidad fija BCL en .NET 8. |
| **.NET 8.0 JIT** | `ListIntDynamic` (Baseline) | 100 | 367.0 ns | 1,184 B | 1.00 | Lista dinámica BCL en .NET 8. |
| | | | | | | |
| **NativeAOT 10.0** | `PooledListIntFixed` | 1000 | **2,369.5 ns** | **0 B** | **0.83** | **17% más rápido que BCL**, cero GC en AOT. |
| **NativeAOT 10.0** | `ListIntFixed` | 1000 | 2,582.0 ns | 4,056 B | 0.90 | Precapacidad BCL (4 KB en heap). |
| **NativeAOT 10.0** | `ListIntDynamic` (Baseline) | 1000 | 2,867.4 ns | 8,424 B | 1.00 | Lista BCL con 8.4 KB en heap. |
| **NativeAOT 10.0** | `PooledListIntDynamic` | 1000 | 2,982.4 ns | **0 B** | 1.04 | **0 B en heap** (ahorro total de 8.4 KB). |
| **.NET 10.0 JIT** | `PooledListIntFixed` | 1000 | **2,366.1 ns** | **0 B** | **0.81** | **19% más rápido que BCL**, 0 B en heap. |
| **.NET 10.0 JIT** | `PooledListIntDynamic` | 1000 | **2,555.7 ns** | **0 B** | **0.87** | **13% más rápido que BCL dinámica**, 0 B en heap. |
| **.NET 10.0 JIT** | `ListIntFixed` | 1000 | 2,627.0 ns | 4,056 B | 0.90 | Precapacidad BCL (4 KB en heap). |
| **.NET 10.0 JIT** | `ListIntDynamic` (Baseline) | 1000 | 2,932.0 ns | 8,424 B | 1.00 | Redimensionamiento BCL con 8.4 KB en heap. |
| **.NET 9.0 JIT** | `PooledListIntFixed` | 1000 | **2,350.2 ns** | **0 B** | **0.79** | **21% más rápido que BCL**, 0 B en heap. |
| **.NET 9.0 JIT** | `PooledListIntDynamic` | 1000 | **2,506.2 ns** | **0 B** | **0.84** | **16% más rápido que BCL**, 0 B en heap. |
| **.NET 9.0 JIT** | `ListIntFixed` | 1000 | 2,652.8 ns | 4,056 B | 0.89 | Precapacidad BCL en .NET 9. |
| **.NET 9.0 JIT** | `ListIntDynamic` (Baseline) | 1000 | 2,982.8 ns | 8,424 B | 1.00 | Lista dinámica BCL en .NET 9. |
| **.NET 8.0 JIT** | `ListIntFixed` | 1000 | 2,671.2 ns | 4,056 B | 0.88 | Precapacidad BCL en .NET 8. |
| **.NET 8.0 JIT** | `PooledListIntFixed` | 1000 | 2,696.9 ns | **0 B** | 0.89 | 11% más rápido que BCL dinámica, 0 B en heap. |
| **.NET 8.0 JIT** | `PooledListIntDynamic` | 1000 | 2,782.9 ns | **0 B** | 0.92 | 8% más rápido que BCL dinámica, 0 B en heap. |
| **.NET 8.0 JIT** | `ListIntDynamic` (Baseline) | 1000 | 3,040.7 ns | 8,424 B | 1.00 | Lista dinámica BCL en .NET 8. |

### Tabla 2: StandardArray vs PooledArray (Medidos Multi-Runtime)
*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled.PooledArrayIntBenchmarks`.

| Runtime / Engine | Método de Prueba | Tamaño (N) | Latencia (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `StandardArray` (Baseline) | 100 | 104.9 ns | 424 B | 1.00 | Asignación en heap BCL. |
| **NativeAOT 10.0** | `PooledArray` | 100 | 196.2 ns | **0 B** | 1.87 | Búfer alquilado de pool, cero GC. |
| **.NET 10.0 JIT** | `StandardArray` (Baseline) | 100 | 113.2 ns | 424 B | 1.00 | Array primitivo en RyuJIT 10. |
| **.NET 10.0 JIT** | `PooledArray` | 100 | 199.1 ns | **0 B** | 1.76 | Cero asignación en heap. |
| **.NET 9.0 JIT** | `StandardArray` (Baseline) | 100 | 125.1 ns | 424 B | 1.00 | Array primitivo en .NET 9. |
| **.NET 9.0 JIT** | `PooledArray` | 100 | 193.1 ns | **0 B** | 1.54 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `StandardArray` (Baseline) | 100 | 124.3 ns | 424 B | 1.00 | Array primitivo en .NET 8 LTS. |
| **.NET 8.0 JIT** | `PooledArray` | 100 | 204.6 ns | **0 B** | 1.65 | Cero asignación en heap. |
| | | | | | | |
| **NativeAOT 10.0** | `StandardArray` (Baseline) | 1000 | 806.7 ns | 4,024 B | 1.00 | Asignación en heap de 4 KB. |
| **NativeAOT 10.0** | `PooledArray` | 1000 | 1,885.3 ns | **0 B** | 2.34 | **0 B de impacto en GC**. |
| **.NET 10.0 JIT** | `StandardArray` (Baseline) | 1000 | 841.7 ns | 4,024 B | 1.00 | 4 KB en heap BCL. |
| **.NET 10.0 JIT** | `PooledArray` | 1000 | 1,661.9 ns | **0 B** | 1.97 | **0 B de impacto en GC**. |
| **.NET 9.0 JIT** | `StandardArray` (Baseline) | 1000 | 993.6 ns | 4,024 B | 1.00 | 4 KB en heap BCL. |
| **.NET 9.0 JIT** | `PooledArray` | 1000 | 1,644.0 ns | **0 B** | 1.66 | **0 B de impacto en GC**. |
| **.NET 8.0 JIT** | `StandardArray` (Baseline) | 1000 | 1,003.2 ns | 4,024 B | 1.00 | 4 KB en heap BCL. |
| **.NET 8.0 JIT** | `PooledArray` | 1000 | 1,903.2 ns | **0 B** | 1.90 | **0 B de impacto en GC**. |

---

## 3. Diagnóstico de Limitaciones y Trade-offs Técnicos (Ingeniería Honesta)

1.  **Tipos de Valor Primitivos (`unmanaged` / `int`, `byte`)**:
    *   **Ventaja**: El rendimiento de las colecciones dinámicas en CPU mejora en un rango del **44.6% al 60.3% (medido)** frente a `List<T>`. La limpieza física del buffer en el pool se omite de forma segura en `Dispose()` mediante `RuntimeHelpers.IsReferenceOrContainsReferences<T>()`.
    *   **Desventaja**: El búfer reutilizado retiene información del uso previo, siendo responsabilidad del consumidor inicializar cada índice antes de leerlo.
2.  **Tipos de Referencia (`class` / `string`)**:
    *   **Desventaja (Penalización de CPU)**: Devolver arrays con referencias requiere limpiar el buffer (`clearArray: true`) para no retener objetos y evitar fugas de memoria. Esto añade un costo significativo. A $N=1000$, `PooledList_String_Dynamic` incrementa su latencia un **55.2% (medido)** respecto a `List<string>` (**4,223.16 ns (medido)** vs **2,720.72 ns (medido)**).
    *   **Ventaja**: Mantiene el perfil **zero-allocation** en el heap (0 bytes frente a los 16,600 bytes del baseline), reduciendo picos de latencia imprevistos de GC.
3.  **Wrappers de Arrays**:
    *   La instanciación de `PooledArray` conlleva una penalización en CPU de hasta un **70.0% (medido)** en comparación con arrays tradicionales de .NET debido al alquiler de buffers y validación contra boxing. Solo se justifica para evitar allocations en flujos de alta frecuencia.
4.  **Copia por valor de las variantes `struct` (`PooledList<T>`, `PooledArray<T>`)**:
    *   **Desventaja (corrupción silenciosa)**: copiar la estructura produce dos valores que comparten el mismo búfer alquilado, mientras que `IsDisposed` es estado **por instancia**: cada copia tiene su propio indicador y la validación deja de proteger. Tras `var b = a; a.Dispose(); b.Dispose();`, dos llamadas independientes a `ArrayPool<T>.Shared.Rent` devuelven **el mismo array (medido)**, de modo que dos consumidores sin relación escriben sobre el mismo búfer.
    *   No basta con copiar y disponer una sola vez: al ampliar, la lista devuelve el array anterior al pool y sustituye el suyo, así que una copia que crece deja a la otra apuntando a memoria ya devuelta.
    *   `ValidarNoBoxeado` **no cubre este caso**, porque la copia ocurre en la pila y no hay boxing que detectar. Se generan copias al asignar a otra variable, al guardar en un campo o propiedad, al pasar por valor a un método y con expresiones `with`. La forma segura de compartir la instancia es aliasarla con `ref` o `in`, que mantiene una sola instancia y devuelve efectividad a `IsDisposed`.
    *   Es la única situación conocida del proyecto en la que el uso incorrecto **no lanza**: falla en silencio y lejos del culpable. Está prevista una regla de análisis estático que lo marque en tiempo de compilación; hasta entonces es responsabilidad del consumidor.
5.  **Variantes basadas en pila (`ref struct` / `PooledListRef<T>`)**:
    *   **Ventaja**: Reducen el tiempo de ejecución en CPU entre un **12.3% y 21.6% (medido)** en comparación con sus equivalentes `struct` tradicionales (ej. `PooledList<T>`). Esto ocurre porque se omite la validación de no-boxing en ejecución y el compilador JIT realiza optimizaciones locales en stack, operando con un layout de apenas 16 bytes.
    *   **Desventaja**: Restricciones de compilador de C#. No se pueden usar en métodos asíncronos (`async await`), no implementan interfaces, y no pueden escapar al heap (no se pueden declarar en campos de clases no-ref).
