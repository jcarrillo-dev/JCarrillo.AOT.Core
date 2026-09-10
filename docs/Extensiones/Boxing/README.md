[Volver al Módulo de Extensiones](../README.md)

# Detección de Boxing en Tiempo de Ejecución (Zero-Allocation Validation)


El espacio de nombres `JCarrillo.AOT.Core.Extensiones.Boxing` (ver [Extensiones/Boxing/](../../../JCarrillo.AOT.Core/Extensiones/Boxing/)) proporciona métodos de extensión para verificar y validar en tiempo de ejecución que los structs de alto rendimiento no sufran boxing ni se ubiquen en el heap.

---

## 1. Funcionamiento Físico de la Detección

El método `ValidarNoBoxeado<T>(this ref T value)` (ver [BoxingExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/Boxing/BoxingExtensions.cs)) opera a nivel de punteros físicos de memoria:

1.  **Puntero de la Estructura**: Obtiene el puntero de memoria administrada de la estructura evaluada mediante `Unsafe.AsPointer(ref value)`.
2.  **Límites de la Pila (Stack) del Hilo**:
    *   **En Windows**: Utiliza P/Invoke nativo (`GetCurrentThreadStackLimits` de `kernel32.dll`) para recuperar los límites de la pila física del hilo actual (`lowLimit` y `highLimit`) de forma rápida.
    *   **En UNIX/macOS/Wasm**: Estima el espacio local del stack frame dinámicamente respecto a una variable local de control del stack.
3.  **Evaluación de Rango**: Compara si la dirección de memoria de la estructura (`thisPtr`) reside fuera de la pila del hilo. Si está fuera, significa que el compilador ha movido el struct al Heap de GC (boxing o asignación normal en clase), lanzando de forma inmediata una excepción `InvalidOperationException`.

---

## 2. Solución a Limitaciones del Compilador (Casos CS8338 y CS9301)

El compilador de C# prohíbe invocar extensiones genéricas basadas en `this ref T` sobre referencias catalogadas como de solo lectura (como el puntero implícito `this` dentro de los métodos de un `readonly record struct`, o parámetros pasados con el modificador `in`). Esto resulta en errores de compilación tales como:
*   **CS8338**: *"The member cannot be used in this context because it may expose referenced variables..."*
*   **CS9301**: *"Cannot pass 'this' as ref or out because it is read-only..."*

Para resolver esto sin perder rendimiento, se implementó un diseño híbrido:
1.  **Firma Genérica Mutable**: `public static void ValidarNoBoxeado<T>(this ref T value) where T : struct` (utilizada en `PooledList<T>` y `PooledArray<T>`).
2.  **Firma Concreta Read-Only**: `public static void ValidarNoBoxeado(this in SemaphoreLock value)` (diseñada específicamente para resolver la restricción de solo lectura en `SemaphoreLock` haciendo uso interno de `Unsafe.AsRef` para obtener de forma segura y veloz la dirección física de la estructura).

---

## 3. Justificación y Costo en Producción (Ingeniería Honesta)

*   **Necesidad**: En C#, las interfaces y expresiones lambda con capturas de contexto pueden boxear structs de forma silenciosa. Esta utilidad diagnostica estas violaciones de forma proactiva en tiempo de ejecución.
*   **Costo de CPU (Medido)**: El método está optimizado con inlining en su fast-path y almacena los límites de pila en una variable estática por hilo (`[ThreadStatic]`), lo que reduce su costo a un valor de apenas nanosegundos por llamada. La llamada al lanzamiento de excepciones se delega a métodos auxiliares no-inlineables (`ThrowBoxingDetected`) para no contaminar la caché de instrucciones del procesador.

---

## 4. Métricas Comparativas de Rendimiento (Medidas con los 6 Runtimes en BenchmarkDotNet)

*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.Extensiones.BoxingBenchmarks` (Baseline = `BoxingHeap`).

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `ValidarNoBoxeadoStack` | **1.52 ns** | **0 B** | **0.75** | Cero asignación y máxima velocidad en AOT nativo. |
| **NativeAOT 10.0** | `BoxingHeap` (Baseline) | 2.02 ns | 24 B | 1.00 | Boxing estándar BCL en heap. |
| **NativeAOT 10.0** | `BoxingInterfaceHeap` | 3.45 ns | 24 B | 1.71 | Boxing de dispatch por interfaz BCL. |
| **.NET 10.0 JIT** | `ValidarNoBoxeadoStack` | **2.18 ns** | **0 B** | **0.49** | RyuJIT 10 con inlining completo y cero allocations. |
| **.NET 10.0 JIT** | `BoxingHeap` (Baseline) | 4.46 ns | 24 B | 1.00 | Boxing estándar BCL en heap. |
| **.NET 10.0 JIT** | `BoxingInterfaceHeap` | 4.68 ns | 24 B | 1.05 | Despacho de interfaz con boxing. |
| **.NET 9.0 JIT** | `ValidarNoBoxeadoStack` | **1.74 ns** | **0 B** | **0.30** | Optimización de stack en .NET 9. |
| **.NET 9.0 JIT** | `BoxingHeap` (Baseline) | 5.77 ns | 24 B | 1.00 | Boxing estándar BCL en heap. |
| **.NET 9.0 JIT** | `BoxingInterfaceHeap` | 5.83 ns | 24 B | 1.01 | Despacho de interfaz con boxing. |
| **.NET 8.0 JIT** | `ValidarNoBoxeadoStack` | **2.29 ns** | **0 B** | **0.48** | Evaluación en .NET 8 LTS. |
| **.NET 8.0 JIT** | `BoxingHeap` (Baseline) | 4.80 ns | 24 B | 1.00 | Boxing estándar BCL en heap. |
| **.NET 8.0 JIT** | `BoxingInterfaceHeap` | 5.46 ns | 24 B | 1.14 | Despacho de interfaz con boxing. |

