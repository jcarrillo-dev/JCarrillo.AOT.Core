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
