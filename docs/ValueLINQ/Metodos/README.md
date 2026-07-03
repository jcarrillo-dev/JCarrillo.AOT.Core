[Volver a ValueLINQ](../README.md)

# Operadores y Extensiones de Consulta de ValueLINQ


Esta sección de la documentación sirve como índice y guía de referencia técnica para todos los operadores de consulta fluent, segmentadores y materializadores disponibles en **ValueLINQ (versión 1.1.0)**. 

A diferencia de LINQ estándar, que depende del heap para albergar delegados, clausuras y enumeradores, los operadores de ValueLINQ están diseñados como structs y métodos genéricos con inlining agresivo. Esto permite la ejecución con **cero asignaciones en el Heap de GC (0 B Allocated)** y un rendimiento predecible en entornos **Native AOT**.

---

## 1. Catálogo de Operadores (v1.1.0)

A continuación se detallan los operadores implementados, divididos por su disponibilidad en los motores Eager y Delay:

### Operadores Universales (Soportados en Eager y Delay)

| Operador / Categoría | Documentación Técnica | Firma Conceptual | Propósito y Trade-offs |
| :--- | :--- | :--- | :--- |
| **Filtrado (`Where`)** | [Where.md](Where.md) | `query.Where(structPredicate)` o `query.Where(func)` | Filtra elementos basándose en un criterio. Permite el inlining completo por el JIT en su variante struct y la comodidad de lambdas en su variante Func. |
| **Proyección (`Select`)** | [Select.md](Select.md) | `query.Select(structSelector)` o `query.Select(func)` | Transforma elementos a un nuevo tipo. Evita asignaciones en heap en la variante struct y simplifica la sintaxis en la variante Func. |

### Operadores Exclusivos de Eager

| Operador / Categoría | Documentación Técnica | Firma Conceptual | Propósito y Trade-offs |
| :--- | :--- | :--- | :--- |
| **Concatenación (`Concat`)** | (Ver [ValueLINQExtensions.cs](file:///F:/Github/JCarrillo.AOT.Core/JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs)) | `query.Concat(otraQuery)` | Une dos colecciones materializando los resultados en un buffer intermedio común de StateManager. |
| **Particionamiento (`Chunk`)** | [Chunk.md](Chunk.md) | `query.Chunk(tamaño).ProcessChunks(structProcessor)` | Divide colecciones en subconsultas `ValueLINQStruct<T>`. El buffer del contenedor externo y de cada chunk se rentan del pool de forma limpia y se liberan mediante `ProcessChunks` bajo bloques `try-finally`. |
| **Materialización / Caching** | [Materializacion.md](Materializacion.md) | `query.ToList()`, `query.ToArray()`, etc. | Copia en bloque los datos transitorios de la sesión a colecciones rápidas de ciclo de vida prolongado (`PooledList<T>`, `PooledArray<T>`) y libera inmediatamente la ranura del StateManager. |

---

## 2. Sobrecargas Ergonómicas y Advertencias de Compilación (JCA0001)

Además de la API basada en structs genéricos para el máximo rendimiento, se implementan sobrecargas ergonómicas que aceptan expresiones lambda (`Func<T, bool>` y `Func<T, TResultado>`).

### Mitigación y el Analizador JCA0001
- **Advertencia JCA0001**: Al utilizar expresiones lambda genéricas de tipo `Func`, se emite una advertencia de compilación **JCA0001** (u obsoleta) para notificar al desarrollador que esta ruta no garantiza cero asignaciones (debido a la instanciación de delegados o clases de clausura generadas por el compilador).
- **Mitigación con Lambdas Estáticas**: Para mitigar las asignaciones del delegado, se recomienda el uso del modificador `static` en la expresión lambda (por ejemplo, `static x => x % 2 == 0`). Esto evita la captura de variables de ámbito local, logrando **0 B (medido)** de asignaciones del delegado al ser cacheado por el runtime, a costa de no poder capturar variables locales externas.

---

## 3. Abstracción Basada en Structs (Patrón de Delegación)

Para lograr un rendimiento óptimo de cero asignaciones y permitir la optimización en tiempo de compilación por el JIT, ValueLINQ utiliza el patrón de structs dedicados que implementan interfaces:

*   **[IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs)**: Utilizada en `Where` para evaluar un predicado con firma `bool Ejecutar(T objetoLista, TDato parametro)`.
*   **[ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs)**: Utilizada en `Select` para transformar tipos con firma `TResult Ejecutar(TOrigen objetoLista, TDato parametro)`.
*   **[IProcesarChunkDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IProcesarChunkDelegado.cs)**: Utilizada en `ProcessChunks` para consumir y liberar subconsultas chunked con firma `void Ejecutar(ValueLINQStruct<T> listaChunk)`.

> [!TIP]
> **Inlining agresivo**:
> Decorar los métodos de ejecución de estos structs con `[MethodImpl(MethodImplOptions.AggressiveInlining)]` asegura que el compilador JIT/AOT incruste la lógica del usuario directamente en el cuerpo del bucle de iteración interna de ValueLINQ, eliminando por completo el costo de la invocación de llamadas virtuales y el overhead de paso de parámetros.
