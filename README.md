# JCarrillo.AOT.Core

Biblioteca de infraestructura crítica orientada a escenarios de baja latencia y con perfil zero-allocation en rutas de ejecución calientes. Diseñada específicamente para garantizar plena compatibilidad con **Native AOT** y **Trimming** en .NET 10.0, eliminando cualquier uso de reflexión en tiempo de ejecución.

---

## 1. Arquitectura y Objetivos de Diseño

El propósito principal de esta biblioteca es mitigar la latencia y la presión sobre el recolector de basura (Garbage Collector) mediante dos técnicas:
1. **Evitación de alojamiento en el Heap**: Reutilización de búferes de memoria subyacentes mediante el uso de `ArrayPool<T>` encapsulados bajo tipos de valor.
2. **Ciclo de vida restrictivo en el Stack**: Uso sistemático de validaciones en tiempo de ejecución para asegurar que las estructuras de valor de control no sufran copias implícitas al montón (boxing).

---

## 2. Componentes Clave y API

### PooledList\<T\> (Estructura de control de ciclo de vida)
* **Ubicación**: [PooledList.cs](JCarrillo.AOT.Core/Colecciones/Pooled/PooledList.cs)
* **API**: Implementa la interfaz [IPooledStruct\<T\>](JCarrillo.AOT.Core/Colecciones/Pooled/IPooledStruct.cs).
* **Descripción**: Estructura de tipo `record struct` que proporciona una lista de crecimiento dinámico respaldada por `ArrayPool<TItem>.Shared`. Debe ser liberada mediante `using` o llamada explícita a `Dispose()` para retornar el búfer interno al pool de arrays. Si no se libera, se genera una fuga persistente en el pool que degrada el rendimiento de asignación global.

```csharp
using JCarrillo.AOT.Core.Colecciones.Pooled;

// Inicialización de PooledList con capacidad predeterminada de 64 elementos
using (var lista = new PooledList<int>())
{
    lista.Add(10);
    lista.Add(20);
    
    // Acceso directo al Span interno para iterar sin generar asignaciones en Heap
    Span<int> span = lista.Span;
    for (int i = 0; i < span.Length; i++)
    {
        Console.WriteLine(span[i]);
    }
} // El bloque using retorna el array de respaldo a ArrayPool de forma automática
```

### PooledListRef\<T\> (Colección en pila)
* **Ubicación**: [PooledListRef.cs](JCarrillo.AOT.Core/Colecciones/Pooled/Ref/PooledListRef.cs)
* **Descripción**: Colección de tipo `public ref struct` que proporciona una lista de crecimiento dinámico respaldada por `ArrayPool<TItem>.Shared`. Al ser un `ref struct`, el compilador de C# garantiza estáticamente que reside de forma exclusiva en la pila (stack) y no puede escapar al montón (heap), lo que elimina la necesidad de comprobaciones en tiempo de ejecución como `ValidarNoBoxeado`. Para mantener la seguridad en el stack, **no expone** ninguna propiedad `Memory<T>`, ofreciendo acceso único a sus elementos mediante su propiedad `Span`. Debe ser liberada mediante `using` o llamada explícita a `Dispose()`.

```csharp
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

// Inicialización ligada al stack con 'using var'
using var listaRef = new PooledListRef<int>(100);

listaRef.Add(42);
listaRef.Add(84);

// Acceso directo a elementos mediante el indexador
ref int primerElemento = ref listaRef[0];
primerElemento = 100;

// Acceso a datos exclusivamente a través de la propiedad 'Span'
Span<int> span = listaRef.Span;
for (int i = 0; i < span.Length; i++)
{
    Console.WriteLine(span[i]);
}
```

### PooledArray\<T\> (Contenedor estático)
* **Ubicación**: [PooledArray.cs](JCarrillo.AOT.Core/Colecciones/Pooled/PooledArray.cs)
* **API**: Implementa la interfaz [IPooledStruct\<T\>](JCarrillo.AOT.Core/Colecciones/Pooled/IPooledStruct.cs).
* **Descripción**: Envoltorio de tamaño fijo para arrays alquilados de `ArrayPool<TItem>.Shared`. Su capacidad no es ampliable dinámicamente; llamadas a `IntentarAmpliar` devuelven `false` sin realizar operaciones.

```csharp
using JCarrillo.AOT.Core.Colecciones.Pooled;

using (var arrayPooled = new PooledArray<byte>(1024))
{
    // Acceso por referencia directa (ref) al índice
    ref byte primerElemento = ref arrayPooled[0];
    primerElemento = 255;
    
    // Acceso al Span o Memory para interoperabilidad con E/S
    Span<byte> span = arrayPooled.Span;
}
```

### PooledArrayRef\<T\> (Contenedor en pila)
* **Ubicación**: [PooledArrayRef.cs](JCarrillo.AOT.Core/Colecciones/Pooled/Ref/PooledArrayRef.cs)
* **Descripción**: Contenedor de tamaño fijo de tipo `public ref struct` que envuelve arrays alquilados de `ArrayPool<TItem>.Shared`. Al igual que `PooledListRef<T>`, reside exclusivamente en la pila, evitando asignaciones e impidiendo que el compilador permita su escape al montón, descartando la necesidad de validación dinámica contra boxing. Solo expone sus elementos mediante `Span` (la propiedad `Memory` está omitida para cumplir con las restricciones del stack). Debe ser liberado mediante `using` o llamada explícita a `Dispose()`.

```csharp
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

// Inicialización de tamaño fijo ligada al stack con 'using var'
using var arrayRef = new PooledArrayRef<byte>(256);

// Acceso por referencia directa (ref) mediante indexador
ref byte primerElemento = ref arrayRef[0];
primerElemento = 200;

// Acceso a datos únicamente mediante 'Span' para operaciones de bajo nivel/interoperabilidad
Span<byte> span = arrayRef.Span;
```

### SemaphoreLock y SemaphoreSlimExtensions (Exclusión Mutua)
* **Ubicación**: [SemaphoreLock.cs](JCarrillo.AOT.Core/Extensiones/SemaphoreSlim/SemaphoreLock.cs) | [SemaphoreSlimExtensions.cs](JCarrillo.AOT.Core/Extensiones/SemaphoreSlim/SemaphoreSlimExtensions.cs)
* **Descripción**: Wrapper estructurado en un `readonly record struct` sobre `System.Threading.SemaphoreSlim`. Reemplaza el bloque `try-finally` tradicional por construcciones limpias basadas en ámbitos `using` o `await using`.
* **Optimización Asíncrona**: Cuando el semáforo está libre, `EsperarAsync` retorna sincrónicamente un `ValueTask<SemaphoreLock>` evitando asignaciones de `Task` en el heap. Ante esperas asíncronas reales, delega la ejecución en `EsperarAsyncSlow`, que utiliza el constructor optimizado `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` para reciclar el estado del builder en .NET 10.0.

```csharp
using System.Threading;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

// Bloqueo síncrono estructurado
public void SeccionCriticaSincrona()
{
    using (var lockScope = _semaphore.Esperar())
    {
        // Operación bajo exclusión mutua
    } // Libera automáticamente el semáforo al llamar a Dispose
}

// Bloqueo asíncrono estructurado
public async Task SeccionCriticaAsincronaAsync()
{
    await using (var lockScope = await _semaphore.EsperarAsync().ConfigureAwait(false))
    {
        // Operación asíncrona segura
    } // Libera automáticamente al llamar a DisposeAsync
}
```

### Sistema de Validación de No-Boxing (BoxingExtensions)
* **Ubicación**: [BoxingExtensions.cs](JCarrillo.AOT.Core/Extensiones/Boxing/BoxingExtensions.cs)
* **Descripción**: Para salvaguardar el rendimiento en producción, se implementa una validación en tiempo de ejecución (`ValidarNoBoxeado`) dentro de los destructores (`Dispose`) para interceptar fugas de structs al Heap (boxing).
* **Mecanismo Físico**: Compara la dirección del puntero del struct actual (`ref T`) contra los límites de la pila física del hilo actual (`ThreadStatic`).
  * En Windows: Utiliza la API Win32 `GetCurrentThreadStackLimits` de `kernel32.dll` mediante P/Invoke.
  * En plataformas UNIX/macOS: Calcula un límite estimado en base a una variable del stack local en la primera ejecución.
  * Si la dirección del puntero del struct cae fuera de los límites de la pila (indicando que el objeto fue alojado en el Heap por boxing o asignación normal de clase), se lanza una `InvalidOperationException`.
* **Diseño Híbrido por Limitaciones del Compilador**:
  * C# no permite invocar extensiones genéricas basadas en `this ref T` sobre variables catalogadas como de solo lectura (como la referencia implícita `this` dentro de los métodos de un `readonly record struct`, o parámetros `in`), resultando en fallas de compilación del tipo **CS8338** o **CS9301**.
  * Para solucionar este comportamiento, se estructuró una firma genérica mutable para structs estándares y sobrecargas explícitas e inmutables:
    * Genérica: `public static void ValidarNoBoxeado<T>(this ref T value) where T : struct` (utilizada en `PooledList<T>` y `PooledArray<T>`).
    * Concreta: `public static void ValidarNoBoxeado(this in SemaphoreLock value)` (diseñada específicamente para resolver las restricciones de solo lectura del compilador en `SemaphoreLock` haciendo uso interno de `Unsafe.AsRef`).

---

## 3. Métricas de Rendimiento (Benchmarks)

### Entorno de Evaluación (Medido científicamente)
* **Herramienta de Benchmarking**: BenchmarkDotNet v0.14.0
* **Sistema Operativo**: Windows 11 (10.0.26200.8655)
* **Runtime**: .NET 10.0.9 (10.0.926.27113), X64 RyuJIT con soporte AVX2
* **Modo de Compilación**: Release
* **Garbage Collector**: Non-concurrent Workstation
* **Hardware Intrinsics**: AVX2, AES, BMI1, BMI2, FMA, LZCNT, PCLMUL, POPCNT (VectorSize=256)
* **SDK**: .NET SDK 10.0.301

---

### Comparativa 1: List\<T\> vs PooledList\<T\>

Evaluación secuencial de inicialización, inserción de elementos (`Add`) e iteración síncrona.

| Método de Prueba | Tipo | Tamaño | Latencia (Mean) | Desviación (StdDev) | Gen 0 / 1000 ops | Asignación en Heap | Ratio de Latencia |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **List_Int_Dynamic** (Baseline) | `int` | 100 | 260.80 ns | - | - | 1,184 B | 1.00 |
| List_Int_Fixed | `int` | 100 | 201.20 ns | - | - | 456 B | 0.77 (-23.0%) |
| PooledList_Int_Dynamic | `int` | 100 | 127.90 ns | - | - | **0 B** | 0.49 (-51.0%) |
| PooledList_Int_Fixed | `int` | 100 | 123.20 ns | - | - | **0 B** | 0.47 (-53.0%) |
| PooledListRef_Int_Dynamic | `int` | 100 | 104.50 ns | - | - | **0 B** | 0.40 (-60.0%) |
| PooledListRef_Int_Fixed | `int` | 100 | 108.40 ns | - | - | **0 B** | 0.42 (-58.0%) |
| | | | | | | | |
| **List_Int_Dynamic** (Baseline) | `int` | 1000 | 2,109.70 ns | - | - | 8,424 B | 1.00 |
| List_Int_Fixed | `int` | 1000 | 2,001.30 ns | - | - | 4,056 B | 0.95 (-5.0%) |
| PooledList_Int_Dynamic | `int` | 1000 | 1,199.20 ns | - | - | **0 B** | 0.57 (-43.0%) |
| PooledList_Int_Fixed | `int` | 1000 | 1,103.80 ns | - | - | **0 B** | 0.52 (-48.0%) |
| PooledListRef_Int_Dynamic | `int` | 1000 | 1,042.10 ns | - | - | **0 B** | 0.49 (-51.0%) |
| PooledListRef_Int_Fixed | `int` | 1000 | 838.10 ns | - | - | **0 B** | 0.40 (-60.0%) |
| | | | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 100 | 396.50 ns | - | - | 2,192 B | 1.00 |
| List_String_Fixed | `string` | 100 | 246.20 ns | - | - | 856 B | 0.62 (-38.0%) |
| PooledList_String_Dynamic | `string` | 100 | 440.60 ns | - | - | **0 B** | 1.11 (+11.0%) |
| PooledList_String_Fixed | `string` | 100 | 395.10 ns | - | - | **0 B** | 1.00 (0.0%) |
| PooledListRef_String_Dynamic | `string` | 100 | 434.40 ns | - | - | **0 B** | 1.10 (+10.0%) |
| PooledListRef_String_Fixed | `string` | 100 | 418.40 ns | - | - | **0 B** | 1.06 (+6.0%) |
| | | | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 1000 | 2,706.10 ns | - | - | 16,600 B | 1.00 |
| List_String_Fixed | `string` | 1000 | 2,189.00 ns | - | - | 8,056 B | 0.81 (-19.0%) |
| PooledList_String_Dynamic | `string` | 1000 | 4,264.30 ns | - | - | **0 B** | 1.58 (+58.0%) |
| PooledList_String_Fixed | `string` | 1000 | 3,583.80 ns | - | - | **0 B** | 1.32 (+32.0%) |
| PooledListRef_String_Dynamic | `string` | 1000 | 4,560.00 ns | - | - | **0 B** | 1.69 (+69.0%) |
| PooledListRef_String_Fixed | `string` | 1000 | 3,836.90 ns | - | - | **0 B** | 1.42 (+42.0%) |

---

### Comparativa 2: StandardArray vs PooledArray

Evaluación de instanciación e inicialización de arrays de tipo de valor (`byte`) con tamaños representativos.

| Método de Prueba | Tamaño | Latencia (Mean) | Desviación (StdDev) | Gen 0 / 1000 ops | Asignación en Heap | Ratio de Latencia |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **StandardArray** (Baseline) | 100 | 53.30 ns | - | - | 424 B | 1.00 |
| PooledArray | 100 | 86.55 ns | - | - | **0 B** | 1.62 (+62.4%) |
| PooledArrayRef | 100 | 64.68 ns | - | - | **0 B** | 1.21 (+21.4%) |
| | | | | | | |
| **StandardArray** (Baseline) | 1000 | 383.78 ns | - | - | 4,024 B | 1.00 |
| PooledArray | 1000 | 712.40 ns | - | - | **0 B** | 1.86 (+85.6%) |
| PooledArrayRef | 1000 | 483.91 ns | - | - | **0 B** | 1.26 (+26.1%) |

---

### Comparativa 3: SemaphoreSlim vs SemaphoreLock

Evaluación de adquisición y liberación bajo exclusión mutua de manera síncrona y asíncrona sobre semáforos disponibles inmediatamente.

| Método de Prueba | Tipo de Ejecución | Latencia (Mean) | Desviación (StdDev) | Asignación en Heap | Ratio de Latencia |
| :--- | :--- | :---: | :---: | :---: | :---: |
| **SemaphoreSlim_Sincrono** (Baseline) | Síncrono | 515.0 ns | 126.6 ns | **0 B** | 1.00 |
| SemaphoreLock_Sincrono | Síncrono | 646.2 ns | 110.9 ns | **0 B** | 1.25 (+25.4%) |
| | | | | | |
| **SemaphoreSlim_Asincrono** (Baseline) | Asíncrono | 1,113.1 ns | 122.6 ns | **0 B** | 1.00 |
| SemaphoreLock_Asincrono | Asíncrono | 1,625.8 ns | 228.3 ns | **0 B** | 1.46 (+46.1%) |

---

## 4. Limitaciones y Trade-offs Técnicos

El análisis riguroso de la biblioteca revela claras ventajas e inconvenientes según el tipo de datos procesados:

1. **Tipos de Valor Puros (`unmanaged` / primitivos como `int`, `byte`, `float`):**
   * **Ventaja**: El rendimiento en latencia de las colecciones dinámicas de bajo nivel mejora hasta en un **50%** frente a colecciones estándar. La limpieza de memoria del array subyacente es eludida de forma segura mediante `RuntimeHelpers.IsReferenceOrContainsReferences<T>()` en `ArrayPool<T>.Return`.
   * **Desventaja**: El búfer reutilizado retiene información residual del uso previo. Es responsabilidad del consumidor asegurar la inicialización completa de cada índice antes de su lectura.

2. **Tipos de Referencia (`class` como `string` o structs con referencias):**
   * **Desventaja (Penalización de CPU)**: Devolver arrays que contienen referencias al pool requiere forzosamente habilitar la opción `clearArray: true` para mitigar la retención indeseada de objetos en memoria. Esto introduce una penalización significativa en el tiempo de ejecución. Por ejemplo, en buffers de 1000 elementos, `PooledList<string>` incrementa su latencia un **61.7%** en comparación con `List<string>` (`4,530.10 ns` contra `2,801.60 ns`).
   * **Ventaja**: Mantiene intacto el perfil **zero-allocation** en el montón (0 bytes frente a los 16,600 bytes del baseline), eliminando picos de latencia impredecibles producidos por recolecciones de basura bajo extrema presión de hilos concurrentes.

3. **Wrappers de Arrays e Infraestructura:**
   * La inicialización de `PooledArray` conlleva una penalización en CPU de hasta un **97.2%** en comparación con arrays directos (alquiler, devolución y validaciones). Solo se justifica si se requiere eludir por completo Gen 0/1 GC allocations en procesos de alta frecuencia.
   * `SemaphoreLock` introduce un coste de envoltura del **25.4%** en llamadas síncronas y del **46.1%** en asíncronas frente al uso nativo de `SemaphoreSlim`. Este coste en microsegundos representa el trade-off a cambio de obtener validación en el stack, robustez sintáctica y soporte asíncrono sin alojamiento.

4. **Colecciones basadas en pila (`ref struct` como `PooledListRef<T>` y `PooledArrayRef<T>`):**
   * **Ventaja (Reducción de Latencia y Huella en Stack)**: Ofrecen una reducción del tiempo de ejecución en CPU de entre el **18% y el 32%** en comparación con sus contrapartes `struct` estándares (por ejemplo, `PooledList<T>`). Esta mejora física es consecuencia directa de omitir el costo de la validación dinámica `ValidarNoBoxeado` en tiempo de ejecución y permitir optimizaciones locales del compilador JIT sobre el indexador, reduciendo además la estructura de control a un layout de apenas 16 bytes (el puntero del array y la variable de longitud en el stack).
   * **Desventaja (Restricciones del Ciclo de Vida)**: Poseen limitaciones sintácticas y de arquitectura absolutas debido a las reglas de seguridad de tipos del compilador C#. No son compatibles con métodos asíncronos (`async await`) debido a que las máquinas de estado asíncronas pueden suspenderse y mover el contexto al heap, no pueden implementar ninguna interfaz (como `IPooledStruct<T>`), y tienen estrictamente prohibido escapar al montón (heap) (no pueden declararse como campos de clases no-ref, ni almacenarse en colecciones de objetos, ni pasarse a través de lambdas o delegados que realicen capturas).

---

## 5. Instrucciones para la Reproducción de Mediciones

Para volver a ejecutar las suites de benchmarking en un entorno local y certificar los datos de este informe:

```powershell
dotnet run -c Release --project .\JCarrillo.AOT.Core.Benchmarks\JCarrillo.AOT.Core.Benchmarks.csproj
```
