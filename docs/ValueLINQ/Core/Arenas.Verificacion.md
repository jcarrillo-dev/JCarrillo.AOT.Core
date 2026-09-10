[Volver a Arenas de Memoria](Arenas.md)

# Verificación del sistema de arenas

Registro de ingeniería del sistema de arenas: qué está probado, con qué contramuestra, qué se midió y qué sigue sin cubrir.

No es documentación de uso. Quien quiera saber **cómo usar arenas** o **por qué existe cada restricción** debe leer [Arenas de Memoria en ValueLINQ](Arenas.md); esto es el cuaderno que respalda lo que aquel afirma.

---

## 1. Dónde viven las pruebas

`JCarrillo.AOT.Core.Tests/ValueLINQ/ValueLINQArenaE2ETests.cs` y `ArenaManagerTests.cs`.

| Bloque | Cubre |
|---|---|
| Motor perezoso | liberación en bloque del buffer de `Chunk`, herencia desde la consulta eager, invariancia de la ruta ambiente, arena ya dispuesta, arenas cruzadas entre receptor y argumentos y solo entre argumentos, conservación de la arena del receptor, rechazo de reasignación y las dos formas encadenadas |
| Reaper | recolección de una arena vacía, respeto de las sesiones vivas, exclusión de las persistentes, respaldo por `UltimoUso` en una arena nunca usada, espera a que se vacíe la última tabla y aislamiento por umbral propio |
| Motor eager | admisión de la ambiente junto a una explícita, conservación de la arena del receptor a lo largo de toda la cadena, sobrecargas de entrada de colecciones (`Span`, `ReadOnlySpan`, `Memory`, `PooledList`, `PooledArray`), sobrecargas simétricas de `PooledArray` y alias retrocompatible `ProcessChunks` |
| Blindaje de aislamiento | validación estricta de `arena.IsViva` en `ToValueQuery` y `ToValueRefQuery`, rechazo de `default(ValueLINQArena)` con `ValueLinqArenaInactivaException` previniendo degradación silenciosa a la arena 0, y rechazo de tokens obsoletos por validación generacional en `StateManager` |
| Contrato de igualdad y telemetría | implementación de `IEquatable<ValueLINQArena>`, operadores `==` y `!=`, `Equals(object?)` sin boxing, `GetHashCode()` uniforme y exposición pública de `Id` para telemetría |
| Escotilla `Marshalling` | mezcla permitida, propagación a operadores posteriores, almacenamiento inalterado, validación de sesión intacta y reasignación de arena aún prohibida |
| Excepciones | las cuatro remiten a su ficha JCE desde el mensaje |

---

## 2. Verificado por mutación

Se comprobó que cada una de estas pruebas **falla** al revertir el comportamiento que protege:

- Liberación en bloque del buffer de `Chunk`.
- Rechazo de arenas cruzadas en `Concat`, con y sin receptor ambiente.
- Rechazo en la sobrecarga variádica del eager (antes de retirarle la restricción).
- En el reaper: uso de `Min` en vez de `Max` al agregar, respaldo por `UltimoUso` y filtro de arenas persistentes.
- Conservación de la arena a lo largo de la cadena, propagando las opciones del argumento en vez de las del receptor.
- Blindaje de inicializadores: lanzamiento de `ValueLinqArenaInactivaException` ante `default(ValueLINQArena)` o arenas inactivas en `ToValueQuery` / `ToValueRefQuery`.
- Comprobación generacional estricta en `StateManager.ObtenerMetadatos(long, int)` y `ObtenerTabla(long)` contra encarnaciones de arenas recicladas.
- Igualdad estructural de `ValueLINQArena`: fallo al contrastar handles con distinto token o generación, y funcionamiento de `==` y `Equals`.
- Las cuatro precondiciones de la tabla de abajo.

El resto de las pruebas son afirmaciones de estado en verde **sin contramuestra**.

### Una prueba por precondición, en su capa

Ninguna prueba de extremo a extremo aísla una comprobación concreta, y no es un defecto: cada capa valida su propia precondición sin confiar en que el llamante lo hiciera, así que ante una arena muerta siempre dispara antes una capa anterior. La consecuencia es que **cada comprobación se verifica invocando directamente la función que la contiene**.

| Capa | Prueba | Mutación que la tumba |
|---|---|---|
| Frontera del `Concat` | mezcla de arenas explícitas | `ValidarArenaUnica` ignora la bandera |
| `IsArenaViva(long)` | distinción de encarnaciones con token forjado | degradarla a la semántica del identificador |
| `ObtenerOCrearTabla` | precondición invocada directamente | quitar la comprobación de generación |
| `ValueLINQChunkDelay` | encarnación anterior con la tabla aún materializada | quitar la comparación del token completo |

El último caso exigía construir unas opciones con un token obsoleto sobre una arena viva, algo imposible a través de las factorías sin agotar los ~4095 identificadores. Se resolvió **sin abrir nada en la librería**: la prueba forja las opciones escribiendo sus campos, que ya eran internos, en lugar de pedir un constructor en crudo. La maniobra invasiva vive en el proyecto de pruebas y el tipo sigue sin exponer ninguna vía sin validar, de modo que ningún fork encuentra una costura que el proyecto no haya decidido sostener.

---

## 3. Coste de validar en `Current`

`Current` valida el token **solo** en `ValueLINQChunkDelay`, no en `ValueLINQSessionEnumerator`. La asimetría se decidió midiendo.

| Enumerador | `Current` se invoca | Coste de validar | Decisión |
|---|---|---|---|
| `ValueLINQSessionEnumerator` | por elemento | de 2.87 ns a 5.26 ns por elemento, **+83% (medido)** | no valida |
| `ValueLINQChunkDelay` | por fragmento | ratio 1.01 con fragmentos de 2 y 16, y 0.94 con 256 **(medido)** | valida |

**Entorno**: BenchmarkDotNet 0.15.8, job `net9.0`, Release, AMD Ryzen 9 3950X, Windows 11 Pro, 64 GB, SDK 10.0.301. Se usó un gemelo temporal de cada enumerador sin la comprobación para compararlos en la misma corrida; ambos gemelos se eliminaron después.

**Derivación**. En el caso de sesión el ratio converge a ~1.85 al crecer el tamaño (1.57 con 100 elementos, 1.77 con 500, 1.92 con 1 000, 1.87 con 10 000 y 1.83 con 100 000), que es lo esperable cuando el coste fijo se amortiza y solo queda el coste por elemento. La pendiente entre 100 y 1 000 da +2.72 ns por elemento y la de 100 000 da +2.39 ns: dos derivaciones independientes de los mismos datos.

En `Chunk` la diferencia queda **por debajo del suelo de ruido**. El 0.94 con fragmentos de 256 sale a favor de la variante validada, lo que no debe leerse como una mejora sino como que el efecto no es medible a esa escala.

---

## 4. Coste de las arenas en ejecución

Medido con `ValueLINQArenaBenchmarks` (consultas, `Size` 100 y 1000) y `ValueLINQArenaLifecycleBenchmarks` (ciclo de vida) en los seis jobs JIT y NativeAOT de net8/9/10. Las filas del motor perezoso solo existen en net9+; en net8 lanzan `PlatformNotSupportedException` y se reportan como NA, igual que en el resto de la suite.

| Qué | Resultado | Cifras de referencia (Size=100) |
|---|---|---|
| Enrutado por arena reutilizada, motor eager | no medible; la variante en arena sale entre el 0% y el 5% mejor en los 6 jobs, dentro del ruido | 424.6 vs 439.0 ns en net10; 435.0 vs 442.7 en NativeAOT 10 **(medido)** |
| Transporte de opciones de arena, motor perezoso | no medible | 180.8 vs 180.6 ns en net10; 190.6 vs 190.4 en NativeAOT 10 **(medido)** |
| `Chunk` reservando su buffer en la arena | no medible; la variante en arena sale un 1–2% mejor en los 6 jobs, dentro del ruido | 334.9 vs 338.6 ns en net10 **(medido)** |
| Alquilar y liberar una arena, un hilo | 53–62 ns, 0 B | los seis runtimes **(medido)** |
| Alquilar y liberar bajo 8 hilos simultáneos | 1.10–1.23 µs por operación (×18–22 sobre el caso aislado), 0 B | `OperationsPerInvoke` de 80 000; el alta y el join de los hilos quedan amortizados por debajo del 1% **(medido)** |
| Arena nueva por consulta (anti-patrón) | 2.2–4.1 µs y 25 792 B por consulta | la materialización de la tabla `(int, arena)` en cada estreno de identificador domina el coste **(medido)** |

**Entorno**: BenchmarkDotNet 0.15.8, Release, AMD Ryzen 9 3950X, Windows 11 Pro, 64 GB, SDK 10.0.301; jobs .NET 8.0/9.0/10.0 y NativeAOT 8.0/9.0/10.0 (los AOT exigen lanzar desde una Developer PowerShell de VS: el enlazador de ilcompiler invoca `vswhere.exe`).

### Por qué las cifras de Size=1000 del JIT no se citan

Con 1 000 elementos, las parejas ambiente/arena del JIT oscilan hasta ±20% **cambiando de dirección entre tandas y entre runtimes**: net9 pasó de +6% a −17% entre dos corridas consecutivas y net8 de +1% a +16%, mientras que en NativeAOT —con disposición de código fija— las mismas parejas quedan clavadas (1 122 vs 1 109 ns en AOT 10). Un coste sistemático de enrutado no cambia de signo entre tandas; la alineación de código y buffers del JIT sí. La igualdad en AOT actúa de experimento de control, así que ninguna cifra de Size=1000 del JIT debe citarse como coste del enrutado.

---

## 5. Sin medir

- **Tamaño de `ValueLINQDelayOptions`**, que pasó de 8 a 16 bytes al añadir las banderas. Se copia una vez por operador, no por elemento, así que son decenas de bytes por consulta; **razonado por estructura, no medido**.
- **Coste del barrido del reaper** y del sellado de vaciado en `TablaSesiones.PushIndice`/`PopIndice`, que ocurre dentro de una sección crítica ya existente en el camino de alquiler y liberación de sesiones.
- **Las cifras de memoria residente** del sistema de arenas: las de `Arenas.md` §5 son derivadas por fórmula. La única medida es la asignación de 25 792 B al materializar la entrada `(int, arena)` (§4).

---

## 6. Sin cubrir

- **El camino positivo del reintento de `Alquilar`**: exigiría un segundo test de agotamiento de capacidad que competiría por identificadores con el que ya existe y volvería intermitentes a ambos.
- **El reciclaje real de un identificador de arena**, que requiere agotar los ~4095 disponibles. La distinción que lo detecta sí está probada (§2), pero no el ciclo completo.

---
[Volver a Arenas de Memoria](Arenas.md)
