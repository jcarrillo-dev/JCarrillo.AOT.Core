[Volver a ValueLINQ](../README.md)

# Operadores y Extensiones de Consulta de ValueLINQ


Esta sección de la documentación sirve como índice y guía de referencia técnica para todos los operadores de consulta fluent, segmentadores y materializadores disponibles en **ValueLINQ (versión 1.1.0)**. 

A diferencia de LINQ estándar, que depende del heap para albergar delegados, clausuras y enumeradores, los operadores de ValueLINQ están diseñados como structs y métodos genéricos con inlining agresivo. Esto permite la ejecución con **cero asignaciones en el Heap de GC (0 B Allocated)** y un rendimiento predecible en entornos **Native AOT**.

---

## 1. Catálogo de Operadores (v1.1.0)

A continuación se detallan los operadores implementados en la versión inicial. Cada enlace dirige a su especificación técnica, sobrecargas y métricas de rendimiento reales:

| Operador / Categoría | Documentación Técnica | Firma Conceptual | Propósito y Trade-offs |
| :--- | :--- | :--- | :--- |
| **Filtrado (`Where`)** | [Where.md](Where.md) | `query.Where(param, structPredicate)` | Filtra elementos usando predicados de tipo struct que implementan `IWhereDelegado<T, TParam>`. Permite el inlining completo por el JIT y evita el boxing de variables del contexto. |
| **Proyección (`Select`)** | [Select.md](Select.md) | `query.Select(param, structSelector)` | Transforma elementos usando mapeadores de tipo struct que implementan `ISelectDelegado<T, TResult, TParam>`. Evita asignaciones de delegados intermedios en el heap. |
| **Particionamiento (`Chunk`)** | [Chunk.md](Chunk.md) | `query.Chunk(tamaño).ProcessChunks(structProcessor)` | Divide colecciones en subconsultas `ValueLINQStruct<T>` de tamaño máximo $S$. El buffer del contenedor externo y de cada chunk se rentan del pool de forma limpia y se liberan mediante `ProcessChunks` bajo bloques `try-finally`. |
| **Materialización / Caching** | [Materializacion.md](Materializacion.md) | `query.ToList()`, `query.ToArray()`, etc. | Copia en bloque los datos transitorios de la sesión a colecciones rápidas de ciclo de vida prolongado (`PooledList<T>`, `PooledArray<T>`) y libera inmediatamente la ranura del `ValueLINQStateManager<T>`. |

---

## 2. Abstracción Basada en Structs (Patrón de Delegación)

Para lograr un rendimiento óptimo de cero asignaciones y permitir la optimización en tiempo de compilación por el JIT, ValueLINQ no acepta expresiones lambda convencionales (`Func<T, bool>` o `Func<T, TResult>`). En su lugar, el cliente debe definir estructuras que implementen interfaces dedicadas:

*   **[IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs)**: Utilizada en `Where` para evaluar un predicado con firma `bool Ejectuar(T objetoLista, TDato parametro)`.
*   **[ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs)**: Utilizada en `Select` para transformar tipos con firma `TResult Ejecutar(TOrigen objetoLista, TDato parametro)`.
*   **[IProcesarChunkDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IProcesarChunkDelegado.cs)**: Utilizada en `ProcessChunks` para consumir y liberar subconsultas chunked con firma `void Ejecutar(ValueLINQStruct<T> listaChunk)`.

> [!TIP]
> **Inlining agresivo**:
> Decorar los métodos de ejecución de estos structs con `[MethodImpl(MethodImplOptions.AggressiveInlining)]` asegura que el compilador JIT/AOT incruste la lógica del usuario directamente en el cuerpo del bucle de iteración interna de ValueLINQ, eliminando por completo el costo de la invocación de llamadas virtuales y el overhead de paso de parámetros.
