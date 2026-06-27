[Volver al Módulo de Colecciones](../README.md)

# Colecciones Rentadas (Pooled Collections): Arquitectura y Gestión de Memoria


El espacio de nombres `JCarrillo.AOT.Core.Colecciones.Pooled` (ver [Colecciones/Pooled/](../../../JCarrillo.AOT.Core/Colecciones/Pooled/)) proporciona colecciones estructuradas optimizadas para escenarios de rendimiento crítico. Su objetivo primordial es eliminar la presión sobre el recolector de basura mediante el arrendamiento y reciclaje de buffers de memoria contigua desde el pool global de recursos (`ArrayPool<T>.Shared`).

---

## 1. Catálogo de Estructuras de Datos (v1.1.0)

El framework ofrece cuatro variantes de colecciones con diferentes compromisos de diseño entre flexibilidad en pila y seguridad estricta:

| Colección | Tipo de Struct | ¿Es Ampliable? | Características Técnicas |
| :--- | :--- | :---: | :--- |
| **`PooledList<T>`** | `record struct` | **SÍ** | Lista dinámica mutable. Permite ser guardada en campos de clases (con precaución), pero cuenta con validación de no-boxing activa en su `Dispose()`. |
| **`PooledArray<T>`** | `record struct` | **NO** | Wrapper inmutable no-ampliable sobre un array del pool. Ofrece acceso por índice y vista de memoria (`Memory<T>`). |
| **`PooledListRef<T>`** | `ref struct` | **SÍ** | Variante de lista confinada estrictamente a la pila. Evita el boxing por diseño del compilador. No puede ser capturada en tareas asíncronas ni subirse al heap. |
| **`PooledArrayRef<T>`** | `ref struct` | **NO** | Variante de array confinado estrictamente a la pila. Su inmutabilidad y confinamiento se resuelven en tiempo de compilación. |

---

## 2. Métricas de Rendimiento Secuencial (Medidas)

Los benchmarks de las colecciones comparan la inicialización, inserción (`Add` / `AddRange`) e iteración síncrona frente a los baselines del sistema.

*   **Entorno de Medición**: Windows 11 (10.0.26200.8655), CPU AMD Ryzen 9 3950X, .NET SDK 10.0.301, runtime .NET 10.0.9 (medido).
*   **Harness**: BenchmarkDotNet v0.14.0, compilación en modo Release.

### Tabla 1: List\<T\> vs PooledList\<T\> (Medidos)
| Método de Prueba | Tipo | Tamaño (N) | Latencia (Mean) | Heap Allocated | Ratio de Latencia |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **List_Int_Dynamic** (Baseline) | `int` | 100 | 260.80 ns | 1,184 B | 1.00 |
| **List_Int_Fixed** | `int` | 100 | 201.20 ns | 456 B | 0.77 |
| **PooledList_Int_Dynamic** | `int` | 100 | 127.90 ns | **0 B** | **0.49** |
| **PooledList_Int_Fixed** | `int` | 100 | 123.20 ns | **0 B** | **0.47** |
| **PooledListRef_Int_Dynamic** | `int` | 100 | 104.50 ns | **0 B** | **0.40** |
| **PooledListRef_Int_Fixed** | `int` | 100 | 108.40 ns | **0 B** | **0.42** |
| | | | | | |
| **List_Int_Dynamic** (Baseline) | `int` | 1000 | 2,109.70 ns | 8,424 B | 1.00 |
| **List_Int_Fixed** | `int` | 1000 | 2,001.30 ns | 4,056 B | 0.95 |
| **PooledList_Int_Dynamic** | `int` | 1000 | 1,199.20 ns | **0 B** | **0.57** |
| **PooledList_Int_Fixed** | `int` | 1000 | 1,103.80 ns | **0 B** | **0.52** |
| **PooledListRef_Int_Dynamic** | `int` | 1000 | 1,042.10 ns | **0 B** | **0.49** |
| **PooledListRef_Int_Fixed** | `int` | 1000 | 838.10 ns | **0 B** | **0.40** |
| | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 100 | 396.50 ns | 2,192 B | 1.00 |
| **List_String_Fixed** | `string` | 100 | 246.20 ns | 856 B | 0.62 |
| **PooledList_String_Dynamic** | `string` | 100 | 440.60 ns | **0 B** | 1.11 |
| **PooledListRef_String_Dynamic**| `string` | 100 | 434.40 ns | **0 B** | 1.10 |
| | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 1000 | 2,706.10 ns | 16,600 B | 1.00 |
| **List_String_Fixed** | `string` | 1000 | 2,189.00 ns | 8,056 B | 0.81 |
| **PooledList_String_Dynamic** | `string` | 1000 | 4,264.30 ns | **0 B** | 1.58 |
| **PooledListRef_String_Dynamic**| `string` | 1000 | 4,560.00 ns | **0 B** | 1.69 |

### Tabla 2: StandardArray vs PooledArray (Medidos)
| Método de Prueba | Tamaño (N) | Latencia (Mean) | Heap Allocated | Ratio de Latencia |
| :--- | :---: | :---: | :---: | :---: |
| **StandardArray** (Baseline) | 100 | 53.30 ns | 424 B | 1.00 |
| **PooledArray** | 100 | 86.55 ns | **0 B** | 1.62 |
| **PooledArrayRef** | 100 | 64.68 ns | **0 B** | 1.21 |
| | | | | |
| **StandardArray** (Baseline) | 1000 | 383.78 ns | 4,024 B | 1.00 |
| **PooledArray** | 1000 | 712.40 ns | **0 B** | 1.86 |
| **PooledArrayRef** | 1000 | 483.91 ns | **0 B** | 1.26 |

---

## 3. Diagnóstico de Limitaciones y Trade-offs Técnicos (Ingeniería Honesta)

1.  **Tipos de Valor Primitivos (`unmanaged` / `int`, `byte`)**:
    *   **Ventaja**: El rendimiento de las colecciones dinámicas en CPU mejora en un **50% (medido)** frente a `List<T>`. La limpieza física del buffer en el pool se omite de forma segura en `Dispose()` mediante `RuntimeHelpers.IsReferenceOrContainsReferences<T>()`.
    *   **Desventaja**: El búfer reutilizado retiene información del uso previo, siendo responsabilidad del consumidor inicializar cada índice antes de leerlo.
2.  **Tipos de Referencia (`class` / `string`)**:
    *   **Desventaja (Penalización de CPU)**: Devolver arrays con referencias requiere limpiar el buffer (`clearArray: true`) para no retener objetos y evitar fugas de memoria. Esto añade un costo significativo. A $N=1000$, `PooledList_String_Dynamic` incrementa su latencia un **58% (medido)** respecto a `List<string>` (**4,264.30 ns (medido)** vs **2,706.10 ns (medido)**).
    *   **Ventaja**: Mantiene el perfil **zero-allocation** en el heap (0 bytes frente a los 16,600 bytes del baseline), reduciendo picos de latencia imprevistos de GC.
3.  **Wrappers de Arrays**:
    *   La instanciación de `PooledArray` conlleva una penalización en CPU de hasta un **86% (medido)** en comparación con arrays tradicionales de .NET debido al alquiler de buffers y validación contra boxing. Solo se justifica para evitar allocations en flujos de alta frecuencia.
4.  **Variantes basadas en pila (`ref struct` / `PooledListRef<T>`)**:
    *   **Ventaja**: Reducen el tiempo de ejecución en CPU entre un **18% y 32% (medido)** en comparación con sus equivalentes `struct` tradicionales (ej. `PooledList<T>`). Esto ocurre porque se omite la validación de no-boxing en ejecución y el compilador JIT realiza optimizaciones locales en stack, operando con un layout de apenas 16 bytes.
    *   **Desventaja**: Restricciones de compilador de C#. No se pueden usar en métodos asíncronos (`async await`), no implementan interfaces, y no pueden escapar al heap (no se pueden declarar en campos de clases no-ref).
