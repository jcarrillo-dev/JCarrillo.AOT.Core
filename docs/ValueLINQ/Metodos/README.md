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
| **Filtrado (`Where`)** | [Where.md](Where.md) | `query.Where(dato, structPredicate)` en Eager, `query.Where(state, ref structPredicate)` en Delay, o `query.Where(func)` en ambos motores | Filtra elementos basándose en un criterio. La variante struct siempre exige el dato/estado de comparación como primer parámetro (se pasa al predicado vía `IWhereDelegado<TOrigen, TDato>.Ejecutar(item, dato)`) y permite el inlining completo por el JIT; en Delay existe además una sobrecarga que solo recibe el estado y crea el predicado por `default` (`Where<TPredicate, TState>(TState state)`). La variante Func aporta la comodidad de las lambdas. |
| **Proyección (`Select`)** | [Select.md](Select.md) | `query.Select(structSelector)` o `query.Select(func)` | Transforma elementos a un nuevo tipo. Evita asignaciones en heap en la variante struct y simplifica la sintaxis en la variante Func. |
| **Particionamiento (`Chunk`)** | [Chunk.md](Chunk.md) | Eager: `query.Chunk(tamaño).ProcesarChunks(structProcessor)` · Delay: `pipeline.Chunk(tamaño).ProcesarChunk(ref structProcessor)` | En Eager divide la colección en subconsultas `ValueLINQStruct<T>`; los buffers del contenedor externo y de cada chunk se rentan del pool y se liberan mediante `ProcesarChunks` bajo bloques `try-finally`. En Delay (requiere .NET 9+) produce fragmentos perezosos como `ReadOnlySpan<T>` sin materializar subconsultas `ValueLINQStruct<T>` intermedias (reutiliza un único buffer de sesión del StateManager), consumidos con los operadores terminales `ProcesarChunk`/`ProcesarChunkRef`, cuyos procesadores implementan `IProcesarChunkRefDelegado<T>` (existe además una sobrecarga ergonómica basada en delegado, marcada con JCA0001). |

### Operadores Exclusivos de Eager

| Operador / Categoría | Documentación Técnica | Firma Conceptual | Propósito y Trade-offs |
| :--- | :--- | :--- | :--- |
| **Concatenación (`Concat`)** | (Ver [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs)) | `query.Concat(otraQuery)` (sobrecargas de 2, 3 y 4 consultas para `ValueLINQRefStruct<T>` y `ValueLINQStruct<T>`) o `query.Concat(params otrasQueries)` (solo `ValueLINQStruct<T>`; usa `ReadOnlySpan` en .NET 9+ y en versiones anteriores emite la advertencia JCA0003 por asignar el array `params`) | Une varias colecciones materializando los resultados en un nuevo buffer de StateManager que hereda la arena del primer operando. Todos los operandos deben pertenecer a la misma arena: si se mezclan consultas de arenas distintas se lanza `ValueLinqArenaCruzadaException` (las consultas vacías con token 0 no imponen arena y se omiten en la validación). Todas las consultas de origen se liberan (Dispose) al finalizar la operación, incluso ante excepción. |
| **Materialización / Caching** | [Materializacion.md](Materializacion.md) | `query.ToList()`, `query.ToArray()`, etc. | Copia en bloque los datos transitorios de la sesión a colecciones rápidas de ciclo de vida prolongado (`PooledList<T>`, `PooledArray<T>`) y libera inmediatamente la ranura del StateManager. |

---

## 2. Sobrecargas Ergonómicas y Advertencias de Compilación (JCA0001)

Además de la API basada en structs genéricos para el máximo rendimiento, se implementan sobrecargas ergonómicas que aceptan expresiones lambda y delegados (`Func<T, bool>`, `Func<T, TResultado>` y `ProcesarChunkDelegado<T>`), correspondientes a `Where`, `Select` y `ProcesarChunk`.

### Mitigación y el Analizador JCA0001
- **Advertencia JCA0001**: Al utilizar expresiones lambda genéricas de tipo `Func`, se emite una advertencia de compilación **JCA0001** (u obsoleta) para notificar al desarrollador que esta ruta no garantiza cero asignaciones (debido a la instanciación de delegados o clases de clausura generadas por el compilador).
- **Mitigación con Lambdas Estáticas**: Para mitigar las asignaciones del delegado, se recomienda el uso del modificador `static` en la expresión lambda (por ejemplo, `static x => x % 2 == 0`). Esto evita la captura de variables de ámbito local, logrando **0 B (medido)** de asignaciones del delegado al ser cacheado por el runtime, a costa de no poder capturar variables locales externas.

---

## 3. Abstracción Basada en Structs (Patrón de Delegación)

Para lograr un rendimiento óptimo de cero asignaciones y permitir la optimización en tiempo de compilación por el JIT, ValueLINQ utiliza el patrón de structs dedicados que implementan interfaces:

*   **[IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs)**: Utilizada en `Where` para evaluar un predicado con firma `bool Ejecutar(TOrigen objetoLista, TDato otro)`.
*   **[ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs)**: Utilizada en `Select` para transformar tipos con firma `TResultado Ejecutar(TOrigen objetoLista)`.
*   **[IProcesarChunkDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IProcesarChunkDelegado.cs)**: Utilizada en `ProcesarChunks` para consumir y liberar subconsultas chunked con firma `void Ejecutar(ValueLINQStruct<T> listaChunk)`.
*   **[IProcesarChunkRefDelegado](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IProcesarChunkDelegado.cs)** (definida en `IProcesarChunkDelegado.cs`): Utilizada en los terminales `ProcesarChunk` y `ProcesarChunkRef` de la canalización perezosa (motor Delay) para consumir los fragmentos generados por `Chunk` con firma `void Ejecutar(ReadOnlySpan<T> listaChunk)`. La variante `ProcesarChunk` exige un procesador `struct` pasado por referencia (permite acumular estado mutable), mientras que `ProcesarChunkRef` acepta procesadores `allows ref struct` pasados por valor.

> [!TIP]
> **Inlining agresivo**:
> Decorar los métodos de ejecución de estos structs con `[MethodImpl(MethodImplOptions.AggressiveInlining)]` asegura que el compilador JIT/AOT incruste la lógica del usuario directamente en el cuerpo del bucle de iteración interna de ValueLINQ, eliminando por completo el costo de la invocación de llamadas virtuales y el overhead de paso de parámetros.
