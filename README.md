# JCarrillo.AOT.Core: Infraestructura para Baja Latencia y Native AOT

[![NuGet Version](https://img.shields.io/nuget/v/JCarrillo.AOT.Core.svg)](https://www.nuget.org/packages/JCarrillo.AOT.Core/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/blob/main/LICENSE)
[![GitHub Repository](https://img.shields.io/badge/GitHub-Repository-black?logo=github)](https://github.com/jcarrillo-dev/JCarrillo.AOT.Core)
[![Documentation](https://img.shields.io/badge/Documentation-Sitemap-green?logo=markdown)](https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/tree/main/docs)

Biblioteca de infraestructura de alto rendimiento diseñada específicamente para entornos restrictivos en .NET (como compilación Native AOT y sistemas con tolerancia crítica a la latencia de recolección de basura). Garantiza un perfil de cero asignaciones en el Heap (0 B Allocated) en las rutas calientes de ejecución de consultas y sincronización.

> [!IMPORTANT]
> **Garantía de Cero Asignaciones y Compatibilidad con Native AOT**:
> Esta biblioteca elimina el uso de reflexión en tiempo de ejecución, la emisión dinámica de código y el despacho dinámico de interfaces en las rutas calientes de ejecución. Todo el procesamiento está estructurado mediante genéricos estáticos y inlining en tiempo de compilación.

---

## Directorio de Documentación

Para acceder a la arquitectura y especificaciones detalladas de cada módulo, consulte las siguientes guías del repositorio:
*   [Directorio General de Documentación](docs/README.md): Índice principal de guías.
*   [ValueLINQ: Consultas Fluent](docs/ValueLINQ/README.md): Especificación del motor de consultas.
*   [Colecciones Rentadas (Pooled Collections)](docs/Colecciones/Pooled/README.md): Estructuras de datos para pila.
*   [Extensiones y Sincronización](docs/Extensiones/README.md): Bloqueos de exclusión mutua y validadores de stack.

> [!NOTE]
> **Navegación de Documentación**:
> Si está visualizando este paquete en NuGet.org, tenga en cuenta que los enlaces locales relativos (como `docs/README.md`) podrían no resolverse de forma correcta en el navegador. Se recomienda acceder a la documentación completa e interactiva de forma directa en el [Repositorio de GitHub](https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/tree/main/docs).

---

## Módulos y Componentes de la Biblioteca

### 1. ValueLINQ: Consultas Fluent en Pila
Motor de consultas síncrono diseñado para entornos Native AOT.
*   **Abstracción en Stack**: Sustituye delegados y lambdas tradicionales (`Func<T, bool>`) por estructuras genéricas que implementan interfaces específicas (`IWhereDelegado`, `ISelectDelegado`), facilitando al compilador el inlining del código del usuario directamente en el bucle de iteración.
*   **Gestión de Estados**: Utiliza `ValueLINQStateManager<T>` para rentar buffers en una tabla estática global de 4096 ranuras concurrentes, protegida por un modelo de bloqueos segmentados (Lock Striping 1 a 1 por slot).
*   **Devolución Segura**: Todos los operadores están encapsulados en bloques `try-finally` que aseguran la devolución automática de los buffers al `ArrayPool` en caso de excepciones durante la iteración.

### 2. Colecciones Rentadas (Pooled Collections)
Estructuras optimizadas para mitigar la presión sobre el recolector de basura (GC) mediante el reciclaje de memoria física.
*   **`PooledList<T>` y `PooledArray<T>`**: Colecciones de tipo `record struct` que administran buffers del pool de arrays e integran validación dinámica de boxing en su método `Dispose()`.
*   **`PooledListRef<T>` y `PooledArrayRef<T>`**: Variantes `ref struct` confinadas a la pila por diseño de compilador, omitiendo validaciones en ejecución y logrando un rendimiento superior de entre el **18% y 32% (medido)** en CPU.

### 3. Extensiones: SemaphoreLock y Detección de Boxing
Utilidades de diagnóstico de bajo nivel y primitivas de exclusión mutua de alta velocidad.
*   **`SemaphoreLock`**: Estructura en pila que envuelve `SemaphoreSlim` permitiendo ámbitos `using var` seguros. Cuenta con una ruta rápida (Fast-Path) con `Wait(0)` que elude la asignación de la máquina de estados del compilador, y una ruta lenta optimizada con `PoolingAsyncValueTaskMethodBuilder` para reciclar tareas.
*   **Detección de Boxing**: `BoxingExtensions.ValidarNoBoxeado` valida en tiempo de ejecución que el struct reside en los límites físicos de la pila de memoria del hilo actual (mediante P/Invoke a `GetCurrentThreadStackLimits` de `kernel32.dll` en Windows), lanzando una excepción si detecta que la estructura se encuentra en el heap.

---

## Política de Deprecación y Advertencias de Rendimiento ([Obsolete])

El proyecto utiliza de manera sistemática el atributo `[Obsolete]` como mecanismo de advertencia y comunicación técnica para los desarrolladores. El significado y las implicaciones de estas anotaciones se rigen por la siguiente política general:

- **Advertencias de Rendimiento (`[Obsolete(..., error: false)]` o `[Obsolete("...", false)]`)**:
  - Si el mensaje de advertencia **no** indica de forma expresa la remoción futura de la API, el método afectado es **completamente estable, funcional y permanecerá indefinidamente en el ensamblado**.
  - Este atributo se emplea en estos casos de manera exclusiva para notificar al desarrollador sobre penalizaciones de rendimiento asociadas (por ejemplo, advertir que un método como `ToArrayStandard` o `ToListStandard` genera Heap Allocations dentro de un contexto de biblioteca zero-allocation, o señalar alternativas óptimas al cambiar de versión del framework, como en la concatenación mediante `params` en .NET 8).
  - Los desarrolladores pueden emplear estos métodos con total seguridad sobre su permanencia si asumen y aceptan los costes de rendimiento asociados.
- **Deprecación Crítica (`[Obsolete(..., error: true)]` o advertencia de remoción)**:
  - Si el mensaje de la advertencia detalla explícitamente que la API será eliminada o si el atributo se configura con `error: true`, los desarrolladores **deben migrar su código**, dado que dicha API dejará de recibir soporte y será eliminada en versiones subsiguientes.
  - Como regla general de ciclo de vida del software en el proyecto, es extremadamente excepcional que una API pase a considerarse crítica (`error: true`) o sea eliminada sin haber transitado antes por un periodo de gracia previo marcado con `error: false` para advertir de su remoción.

---

## Resultados de Benchmarks

*   **Entorno**: Windows 11, CPU AMD Ryzen 9 3950X, .NET SDK 10.0.301, runtime .NET 10.0.9 (medido).
*   **Harness**: BenchmarkDotNet v0.15.8, compilación en modo Release.

### 1. Desempeño en Native AOT 10.0 (Consultas Fluent a escala $N = 1000$)
En la prueba `Where_Select` (filtrar y proyectar en cadena), la resolución dinámica de interfaces de LINQ estándar sufre una regresión de rendimiento en .NET 10.0. ValueLINQ, al estar resuelto de forma estática en compilación, mantiene un perfil de latencia estable:

*   **Standard LINQ**: **11,946.24 ns (11.9 µs) (medido)** | Heap Allocated: **144 B (medido)**
*   **ValueLINQStruct**: **1,522.53 ns (1.5 µs) (medido)** | Heap Allocated: **0 B (medido)** (7.84x más rápido)
*   **ValueLINQRefStruct**: **1,504.89 ns (1.5 µs) (medido)** | Heap Allocated: **0 B (medido)** (7.93x más rápido)

### 2. Población en Bloque vs Inserción Unitaria ($N = 1000$)
Comparativa entre la población iterativa (que adquiere $O(N)$ locks) y la población en bloque (`Añadir(ReadOnlySpan<T>)` que ejecuta un único lock $O(1)$ con copia vectorial):

*   **ValueLINQStruct (Población Unitaria)**: **32,838.03 ns (medido)** | Heap: **0 B (medido)**
*   **ValueLINQStruct (Población en Bloque)**: **178.29 ns (medido)** | Heap: **0 B (medido)** (184x más rápido)
*   **List<int> (Población en Bloque)**: **231.76 ns (medido)** | Heap: **4,056 B (medido)** (ValueLINQ es 35% más rápido y sin allocations)

> [!NOTE]
> **Información Adicional**:
> Para ver todas las comparativas detalladas de iteración, concatenación y sobrecarga de `params` en múltiples runtimes, consulte el [Reporte de Benchmarks Completo](docs/ValueLINQ/BENCHMARK.md).
