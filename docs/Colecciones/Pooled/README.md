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
| **List_Int_Dynamic** (Baseline) | `int` | 100 | 248.25 ns | 1,184 B | 1.00 |
| **List_Int_Fixed** | `int` | 100 | 197.43 ns | 456 B | 0.80 |
| **PooledList_Int_Dynamic** | `int` | 100 | 124.62 ns | **0 B** | **0.50** |
| **PooledList_Int_Fixed** | `int` | 100 | 122.55 ns | **0 B** | **0.49** |
| **PooledListRef_Int_Dynamic** | `int` | 100 | 102.11 ns | **0 B** | **0.41** |
| **PooledListRef_Int_Fixed** | `int` | 100 | 96.92 ns | **0 B** | **0.39** |
| | | | | | |
| **List_Int_Dynamic** (Baseline) | `int` | 1000 | 2,087.96 ns | 8,424 B | 1.00 |
| **List_Int_Fixed** | `int` | 1000 | 1,850.63 ns | 4,056 B | 0.89 |
| **PooledList_Int_Dynamic** | `int` | 1000 | 1,155.92 ns | **0 B** | **0.55** |
| **PooledList_Int_Fixed** | `int` | 1000 | 1,058.28 ns | **0 B** | **0.51** |
| **PooledListRef_Int_Dynamic** | `int` | 1000 | 1,013.29 ns | **0 B** | **0.49** |
| **PooledListRef_Int_Fixed** | `int` | 1000 | 829.59 ns | **0 B** | **0.40** |
| | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 100 | 384.88 ns | 2,192 B | 1.00 |
| **List_String_Fixed** | `string` | 100 | 262.39 ns | 856 B | 0.68 |
| **PooledList_String_Dynamic** | `string` | 100 | 430.05 ns | **0 B** | 1.12 |
| **PooledListRef_String_Dynamic**| `string` | 100 | 437.35 ns | **0 B** | 1.14 |
| | | | | | |
| **List_String_Dynamic** (Baseline) | `string` | 1000 | 2,720.72 ns | 16,600 B | 1.00 |
| **List_String_Fixed** | `string` | 1000 | 2,282.80 ns | 8,056 B | 0.84 |
| **PooledList_String_Dynamic** | `string` | 1000 | 4,223.16 ns | **0 B** | 1.55 |
| **PooledListRef_String_Dynamic**| `string` | 1000 | 4,533.38 ns | **0 B** | 1.67 |

### Tabla 2: StandardArray vs PooledArray (Medidos)
| Método de Prueba | Tamaño (N) | Latencia (Mean) | Heap Allocated | Ratio de Latencia |
| :--- | :---: | :---: | :---: | :---: |
| **StandardArray** (Baseline) | 100 | 53.75 ns | 424 B | 1.00 |
| **PooledArray** | 100 | 77.23 ns | **0 B** | 1.44 |
| **PooledArrayRef** | 100 | 66.63 ns | **0 B** | 1.24 |
| | | | | |
| **StandardArray** (Baseline) | 1000 | 364.27 ns | 4,024 B | 1.00 |
| **PooledArray** | 1000 | 619.42 ns | **0 B** | 1.70 |
| **PooledArrayRef** | 1000 | 505.45 ns | **0 B** | 1.39 |

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
4.  **Variantes basadas en pila (`ref struct` / `PooledListRef<T>`)**:
    *   **Ventaja**: Reducen el tiempo de ejecución en CPU entre un **12.3% y 21.6% (medido)** en comparación con sus equivalentes `struct` tradicionales (ej. `PooledList<T>`). Esto ocurre porque se omite la validación de no-boxing en ejecución y el compilador JIT realiza optimizaciones locales en stack, operando con un layout de apenas 16 bytes.
    *   **Desventaja**: Restricciones de compilador de C#. No se pueden usar en métodos asíncronos (`async await`), no implementan interfaces, y no pueden escapar al heap (no se pueden declarar en campos de clases no-ref).
