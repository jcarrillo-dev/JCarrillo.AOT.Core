[Volver a Arenas de Memoria](Arenas.md)

# Verificación del sistema de arenas

Registro de ingeniería del sistema de arenas: qué está probado, con qué contramuestra, qué se midió y qué sigue sin cubrir.

No es documentación de uso. Quien quiera saber **cómo usar arenas** o **por qué existe cada restricción** debe leer [Arenas de Memoria en ValueLINQ](Arenas.md); esto es el cuaderno que respalda lo que aquel afirma.

---

## 1. Dónde viven las pruebas

El arnés de pruebas y verificación reside en el proyecto `JCarrillo.AOT.Core.Tests`:
- `JCarrillo.AOT.Core.Tests/Diagnostico/ArrayPoolDiagnosticsListener.cs`: Infraestructura de telemetría y escucha física de eventos de búfer sobre la BCL.
- `JCarrillo.AOT.Core.Tests/ValueLINQ/ValueLINQArenaE2ETests.cs`: Pruebas de integración, ciclo de vida, herencia, motor perezoso y aserciones físicas de extremo a extremo.
- `JCarrillo.AOT.Core.Tests/ValueLINQ/ValueLINQArenaPoolingTests.cs`: Pruebas de contención multihilo, verificación de 0 B en el montón en régimen estacionario, acotamiento del pool y resistencia adversarial anti-ABA.
- `JCarrillo.AOT.Core.Tests/ValueLINQ/ArenaManagerTests.cs`: Pruebas del gestor global de arenas y barrido del reaper.
- `JCarrillo.AOT.Core.Tests/E2E/AllocationAssert.cs`: Utilidad de certificación de cero asignaciones en el montón (`GC.GetAllocatedBytesForCurrentThread`).

| Bloque | Archivo principal | Cubre |
|---|---|---|
| Motor perezoso | `ValueLINQArenaE2ETests.cs` | liberación en bloque del buffer de `Chunk`, herencia desde la consulta eager, invariancia de la ruta ambiente, arena ya dispuesta, arenas cruzadas entre receptor y argumentos y solo entre argumentos, conservación de la arena del receptor, rechazo de reasignación y las dos formas encadenadas |
| Reaper y concurrencia de fondo | `ValueLINQArenaE2ETests.cs`, `ArenaManagerTests.cs` | recolección de una arena vacía, respeto de las sesiones vivas, exclusión de las persistentes, respaldo por `UltimoUso` en una arena nunca usada, espera a que se vacíe la última tabla, aislamiento por umbral propio, y guarda atómica de reentrada (`_isRecolectando`) anti *spin-storm* |
| Motor eager | `ValueLINQArenaE2ETests.cs` | admisión de la ambiente junto a una explícita, conservación de la arena del receptor a lo largo de toda la cadena, sobrecargas de entrada de colecciones (`Span`, `ReadOnlySpan`, `Memory`, `PooledList`, `PooledArray`), sobrecargas simétricas de `PooledArray` y alias retrocompatible `ProcessChunks` |
| Blindaje de aislamiento y contratos | `ValueLINQArenaE2ETests.cs` | validación estricta de `arena.IsViva` en `ToValueQuery` y `ToValueRefQuery`, rechazo de `default(ValueLINQArena)` con `ValueLinqArenaInactivaException` previniendo degradación silenciosa a la arena 0, y rechazo de tokens obsoletos por validación generacional en `StateManager` |
| Contrato de igualdad y telemetría | `ValueLINQArenaE2ETests.cs` | implementación de `IEquatable<ValueLINQArena>`, operadores `==` y `!=`, `Equals(object?)` sin boxing, `GetHashCode()` uniforme y exposición pública de `Id` para telemetría |
| Escotilla `Marshalling` | `ValueLINQArenaE2ETests.cs` | mezcla permitida, propagación a operadores posteriores, almacenamiento inalterado, validación de sesión intacta y reasignación de arena aún prohibida |
| Excepciones | `ValueLINQArenaE2ETests.cs` | las cuatro remiten a su ficha JCE desde el mensaje |
| Telemetría física y simetría de búferes | `ArrayPoolDiagnosticsListener.cs`, `ValueLINQArenaE2ETests.cs` | suscripción a `System.Buffers.ArrayPoolEventSource`, balance físico estricto `devueltosDelta == rentadosDelta`, detección de fugas en pipelines `ToList`, `ToArray`, `Chunk`, contención de 16 hilos, disposición anticipada y limpiezas del reaper |
| Pooling de tablas y 0 B en régimen estacionario | `ValueLINQArenaPoolingTests.cs` | reutilización de instancias limpias de `TablaSesiones<T>`, erradicación de asignaciones en el montón (0 B medido tras calentamiento) a lo largo de 1, 20, 50, 100 y 150 ciclos consecutivos |
| Concurrencia extrema y contención multihilo | `ValueLINQArenaPoolingTests.cs` | contención sincronizada con `Barrier` de 32 hilos concurrentes (1 280 ciclos), verificación de ausencia de interbloqueos, integridad aritmética de datos y reciclaje cruzado productor-consumidor |
| Acotamiento de memoria bajo sobrecarga | `ValueLINQArenaPoolingTests.cs` | saturación determinista del pool en 32 instancias ante ráfagas de 48 arenas paralelas, delegando el exceso a recolección estándar sin retención infinita |
| Falsificación adversarial anti-ABA | `ValueLINQArenaPoolingTests.cs` | preservación monotónica incremental de la versión en slots reciclados (`versionNueva == versionPrevia + 1`), invalidación de tokens residuales de arenas dispuestas o generaciones recicladas |

---

## 2. Arnés de diagnóstico físico: `ArrayPoolDiagnosticsListener`

Para certificar empíricamente que el sistema de arenas y el reciclaje de tablas no producen fugas de recursos en el gestor de memoria compartida de la BCL (`ArrayPool<T>.Shared`), se implementó un arnés de telemetría de eventos de bajo nivel en `JCarrillo.AOT.Core.Tests/Diagnostico/ArrayPoolDiagnosticsListener.cs`.

### 2.1. Arquitectura del escuchador y suscripción a eventos

El escuchador deriva de `System.Diagnostics.Tracing.EventListener` y se conecta a la fuente nativa del runtime:
- **Fuente de eventos objetivo**: `"System.Buffers.ArrayPoolEventSource"`.
- **Nivel y palabras clave**: `EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All)`.
- **Captura atómica de eventos en `OnEventWritten`**:
  - `EventId == 1` (`BufferRented`): `Interlocked.Increment(ref _bufferRentedCount)`
  - `EventId == 2` (`BufferAllocated`): `Interlocked.Increment(ref _bufferAllocatedCount)`
  - `EventId == 3` (`BufferReturned`): `Interlocked.Increment(ref _bufferReturnedCount)`

Las lecturas públicas (`BufferRentedCount` / `Rentados`, `BufferReturnedCount` / `Devueltos`, `BufferAllocatedCount` / `Asignados` y `ActiveBuffersCount` / `Activos`) emplean `Interlocked.Read` con directiva `[MethodImpl(MethodImplOptions.AggressiveInlining)]` para garantizar coherencia en arquitecturas débilmente ordenadas sin introducir contención de cerrojos.

### 2.2. Captura de instantáneas y deltas inmutables

Para garantizar que el diagnóstico no altere las mediciones de memoria, el escuchador define dos tipos asignados en pila:
1. `InstantaneaArrayPool`: `readonly record struct` de 24 bytes que almacena una instantánea inmutable de los tres contadores (`Rentados`, `Devueltos`, `Asignados`). Implementa `CalcularDelta(in InstantaneaArrayPool posterior)` para computar la variación neta.
2. `AmbitoDiagnosticoArrayPool`: `readonly struct` que implementa `IDisposable` (patrón RAII en pila) para capturar la instantánea inicial en su construcción y exponer `ObtenerDelta()` sin instanciar objetos en el montón.

### 2.3. Aislamiento secuencial de pruebas (`DisableParallelization`)

Dado que `EventListener` en .NET es un mecanismo a escala de todo el proceso (captura eventos emitidos por cualquier hilo del AppDomain), la ejecución concurrente de pruebas unitarias contaminaría las mediciones físicas si dos pruebas alquilaran búferes simultáneamente.

Para garantizar aislamiento hermético:
- Se declaró la colección de pruebas en `ValueLINQArenaE2ETests.cs`:
  ```csharp
  [CollectionDefinition("ArrayPoolDiagnostics", DisableParallelization = true)]
  public class ArrayPoolDiagnosticsCollectionDefinition { }
  ```
- Todas las clases que interactúan con el escuchador (`ValueLINQArenaE2ETests` y `ValueLINQArenaPoolingTests`) están decoradas con `[Collection("ArrayPoolDiagnostics")]`.
- El ejecutor de pruebas garantiza la ejecución estrictamente secuencial de las suites de diagnóstico de búferes, eliminando interferencias cruzadas.

---

## 3. Principios metodológicos de prueba

El diseño de las pruebas sigue tres principios estrictos de falsificación y rigor experimental:

### 3.1. Principio de Anti-Vacuidad (`rentadosDelta > 0`)

En pruebas de ciclo de vida físico, una aserción de balance simétrico:
```csharp
_ = devueltosDelta.Should().Be(rentadosDelta);
```
puede satisfacerse de manera trivial y vacía si `rentadosDelta == 0` y `devueltosDelta == 0` (por ejemplo, si el pipeline no requirió búfer, si una optimización elidió el código, o si una excepción temprana impidió la ejecución).

Para falsificar la vacuidad, **toda prueba que certifique balance físico exige como precondición obligatoria**:
```csharp
_ = rentadosDelta.Should().BeGreaterThan(0, "debió ocurrir al menos un alquiler físico en el ArrayPool");
_ = devueltosDelta.Should().Be(rentadosDelta, "todos los buffers físicos alquilados deben retornar íntegramente");
```
Si el sistema no interactuó físicamente con el pool, la prueba falla de inmediato.

### 3.2. Inmunidad al Efecto Observador (Observer Effect Immunity)

Las librerías de aserción (como `FluentAssertions`), las cadenas formateadas (`$"..."`), la captura de trazas de pila y los registradores de consola asignan memoria y pueden alquilar arrays internamente en `ArrayPool` (por ejemplo, al formatear buffers de caracteres o serializar grafos de objetos).

Si una aserción se ejecuta dentro del intervalo de medición o si la instantánea se captura después de evaluar aserciones intermedias, los eventos del marco de pruebas distorsionarían las métricas físicas.

**Metodología aplicada**:
1. Ejecutar el código bajo prueba y disponer los recursos.
2. Capturar inmediatamente la instantánea final `fin = listener.ObtenerInstantanea();`.
3. Calcular los deltas en variables locales de tipo primitivo (`long rentadosDelta`, `long devueltosDelta`).
4. Cerrar el ámbito del escuchador (`using`).
5. **Solo entonces** ejecutar las aserciones de `FluentAssertions`.

Esto garantiza que el instrumento de medida no perturba el fenómeno medido.

### 3.3. Certificación de Cero Asignaciones en el Montón (0 B)

Para verificar el cumplimiento del contrato *Zero-Allocation* en estado estacionario, se utiliza la utilidad `AllocationAssert.AssertZeroAllocations` (`JCarrillo.AOT.Core.Tests/E2E/AllocationAssert.cs`) y lecturas directas de `GC.GetAllocatedBytesForCurrentThread()`:

```csharp
// 1. Calentamiento: ejecución previa para inicializar tipos estáticos y jitteo
action();

// 2. Sincronización del recolector de basura
GC.Collect();
GC.WaitForPendingFinalizers();

// 3. Medición sobre el hilo actual
long before = GC.GetAllocatedBytesForCurrentThread();
action();
long after = GC.GetAllocatedBytesForCurrentThread();

// 4. Aserción estricta de 0 bytes
_ = (after - before).Should().Be(0, "la acción debe ejecutarse con cero asignaciones en el montón");
```

Este procedimiento se aplica no solo a iteraciones individuales, sino a suites de estrés de 50, 100 y 150 ciclos consecutivos (`CicloArenaEstacionarioExtendidoCienCiclosAsignaCeroBytesEnElMonton`), garantizando que la ausencia de asignaciones no es un artefacto de una sola pasada.

---

## 4. Escenarios de estrés y concurrencia extrema

Las garantías de concurrencia y ausencia de fugas se someten a cuatro escenarios de estrés adversarial en `ValueLINQArenaPoolingTests.cs` y `ValueLINQArenaE2ETests.cs`:

### 4.1. Contención multihilo: 32 hilos concurrentes (1 280 ciclos)

- **Escenario**: `ContencionMultihiloAlquilerYConsultaConcurrenteSinBloqueoNiCorrupcion` (`ValueLINQArenaPoolingTests.cs:159`).
- **Configuración**: 32 tareas concurrentes sincronizadas mediante `Barrier(32)`. Cada hilo ejecuta 40 ciclos completos de creación de arena, consulta ValueLINQ con `Where` y `Select`, enumeración acumulativa y disposición de arena (total: 1 280 ciclos).
- **Validación**:
  - Detección de interbloqueos: tiempo de espera de 30 segundos mediante `Task.WhenAny(completada, timeout)`.
  - Integridad aritmética: comprobación de `suma == sumaEsperada` para cada hilo según su identificador único.
  - Cero excepciones concurrentes reportadas en `ConcurrentBag<Exception>`.

### 4.2. Acotamiento del pool bajo sobrecarga: 48 arenas concurrentes

- **Escenario**: `PoolTablasNoSuperaCapacidadAcotadaDeTreintaYDosBajoSobrecarga` (`ValueLINQArenaPoolingTests.cs:211`).
- **Configuración**: Vaciado previo del pool (`ValueLINQStateManager<T>.VaciarPool()`). 48 hilos sincronizados mediante `Barrier(48)` crean simultáneamente 48 arenas y solicitan metadatos para un tipo aislado (`TipoSobrecargaAislada`), forzando la materialización de 48 instancias de `TablaSesiones<T>`. A continuación, una segunda barrera sincroniza la disposición simultánea de las 48 arenas.
- **Validación**:
  - Al disponerse las arenas, `LiberarTablasDeArena` recicla las instancias verificando `_poolTablas.Count < CapacidadMaximaPool` (32).
  - La aserción verifica `ValueLINQStateManager<TipoSobrecargaAislada>.TablasEnPool.Should().Be(32)`.
  - Las 16 instancias sobrantes se descartan para recolección estándar por el GC, demostrando que el pool no crece de forma desmedida en escenarios de ráfaga.

### 4.3. Falsificación adversarial del ataque ABA mediante versiones monotónicas

- **Escenario**: `ReiniciarConservaVersionMonotonicaEnSlotsRecicladosImpideAba` (`ValueLINQArenaPoolingTests.cs:429`).
- **Problema abordado**: En un pool de tablas recicladas, una misma instancia de `TablaSesiones<T>` se reasigna a nuevas arenas. Si una ranura (slot) se libera y se vuelve a ocupar, un token residual en poder de código cliente antiguo podría acceder a la nueva sesión si coincidieran el identificador de ranura y arena.
- **Mecanismo de defensa**: Al reciclar la tabla en `Reiniciar(nuevoIdArena, nuevaArenaGen)`, los contadores de versión de cada ranura (`metadato.Version`) **no se resetean a cero**, sino que se preservan y se incrementan de forma estrictamente monótona (`++metadato.Version`).
- **Validación experimental**:
  - Se simulan 20 ciclos sucesivos de alquiler y reinicio de tabla reutilizando deterministamente la misma ranura del tope del stack (`slotIndexNuevo == slotIndex1`).
  - Se comprueba que `versionNueva > versionPrevia` y que `versionNueva == versionPrevia + 1`.
  - Se verifica que el token de la encarnación previa es rechazado de inmediato (`tabla.IsMetadatoValido(tokenPrevio).Should().BeFalse()`).
  - Pruebas complementarias (`ConsultaResidualDeArenaDispuestaNoPuedeInteractuarConTablaReciclada` y `ColisionMismoIdArenaConGeneracionIncrementadaRechazaTokenAntiguo`) confirman que cualquier intento de acceso, mutación o liberación con tokens residuales lanza la excepción correspondiente sin alterar las sesiones activas.

### 4.4. Aislamiento en reciclaje cruzado entre hilos

- **Escenario**: `ReciclajeConcurrenteEntreHilosCruzadosNoMezclaSesiones` (`ValueLINQArenaPoolingTests.cs:253`).
- **Configuración**: 20 rondas de sincronización productor-consumidor donde dos hilos alternan la creación, ejecución y liberación de arenas en paralelo con un paso de cesión de CPU (`Thread.Yield()`), reutilizando tablas del pool común.
- **Validación**: Cero interferencias o lecturas cruzadas de datos entre las arenas de los dos hilos (`errores.Should().BeEmpty()`).

### 4.5. Simetría física de búferes en ArrayPool (`devueltos == rentados`)

La simetría física de buffers fue certificada en las siguientes rutas críticas:
1. `MaterializarToListEnArenaAlquilaYDevuelveFisicamenteLosBuffersAlArrayPool` (`ValueLINQArenaE2ETests.cs:297`): `ToList()` alquila tanto el buffer de la sesión de consulta como el de `PooledList<T>`; al disponerse ambos, `devueltosDelta == rentadosDelta`.
2. `MaterializarToArrayEnArenaAlquilaYDevuelveFisicamenteLosBuffersAlArrayPool` (`ValueLINQArenaE2ETests.cs:330`): `ToArray()` sobre `PooledArray<T>` garantiza retorno total.
3. `ProcesarChunksEnArenaLiberaFisicamenteTodosLosBuffersDeFragmentosAlArrayPool` (`ValueLINQArenaE2ETests.cs:363`): `Chunk(2)` genera fragmentos secuenciales en buffers rentados; al concluir el delegado, todos los buffers retornan al pool.
4. `MultiplesConsultasConcurrentesEnMismaArenaNoDejanBuffersHuerfanosEnArrayPool` (`ValueLINQArenaE2ETests.cs:393`): 16 consultas concurrentes heterogéneas (`Where` + `ToList` y `Select` + `ToArray`) en la misma arena devuelven el 100% de los búferes al disponer la arena.
5. `DisponerArenaAnticipadamenteConConsultasSinConsumirDevuelveFisicamenteLosBuffersAlArrayPool` (`ValueLINQArenaE2ETests.cs:440`): Consultas abandonadas a medio procesar devuelven físicamente sus búferes al disponer la arena padre. Además, se verifica inmunidad a doble devolución (`dobleDevolucionDelta == 0`) al invocar `Dispose()` posterior sobre las consultas huérfanas.
6. `RecolectarArenasBarridasPorInactividadDevuelveFisicamenteLosBuffersAlArrayPool` (`ValueLINQArenaE2ETests.cs:497`): El barrido de fondo del reaper devuelve físicamente los búferes de las arenas inactivas.
7. `DisponerArenaConChunkDelaySinConsumirDevuelveFisicamenteElBufferAlArrayPool` (`ValueLINQArenaE2ETests.cs:710`): En el motor perezoso, `ChunkDelay` alquila un buffer al instanciarse; la disposición anticipada de la arena restituye el buffer al pool.
8. `ReiniciarLimpiaCompletamenteMetadatosYRestablecePilaDeIndices` (`ValueLINQArenaPoolingTests.cs:345`): Invocar `Reiniciar` directamente sobre una tabla con ranuras ocupadas sin llamar a `LiberarTodo` devuelve físicamente los buffers pendientes sin generar buffers huérfanos en `ArrayPool`.

---

## 5. Verificado por mutación

Se comprobó que cada una de estas pruebas **falla** al revertir el comportamiento que protege:

- Liberación en bloque del buffer de `Chunk`.
- Rechazo de arenas cruzadas en `Concat`, con y sin receptor ambiente.
- Rechazo en la sobrecarga variádica del eager (antes de retirarle la restricción).
- En el reaper: uso de `Min` en vez de `Max` al agregar, respaldo por `UltimoUso` y filtro de arenas persistentes.
- Conservación de la arena a lo largo de la cadena, propagando las opciones del argumento en vez de las del receptor.
- Blindaje de inicializadores: lanzamiento de `ValueLinqArenaInactivaException` ante `default(ValueLINQArena)` o arenas inactivas en `ToValueQuery` / `ToValueRefQuery`.
- Comprobación generacional estricta en `StateManager.ObtenerMetadatos(long, int)` y `ObtenerTabla(long)` contra encarnaciones de arenas recicladas.
- Igualdad estructural de `ValueLINQArena`: fallo al contrastar handles con distinto token o generación, y funcionamiento de `==` y `Equals`.
- Guarda atómica anti *spin-storm* en `RecolectarArenas`: revertir `Interlocked.CompareExchange(ref _isRecolectando, 1, 0)` permite reentradas concurrentes descontroladas durante el barrido.
- Remediación de carrera Reaper-Sesión en `TablaSesiones.InicializarSlot`: retirar la comprobación `if (_arenaId != 0 && (!ValueLINQArenaManager.IsArenaViva(_arenaId) || ValueLINQArenaManager.ObtenerGeneracion(_arenaId) != _arenaGen))` dentro del spinlock de partición provoca que se alquilen buffers en `ArrayPool` sobre arenas que acaban de ser desactivadas concurrentemente por el reaper, produciendo buffers huérfanos y fallando `devueltosDelta == rentadosDelta`.
- Incremento monotónico de versión en `Reiniciar`: omitir `++metadato.Version` o restablecer la versión a cero permite que tokens residuales colisionen con nuevas sesiones (falla la detección anti-ABA).
- Acotamiento de capacidad de reciclaje: retirar la guarda `_poolTablas.Count < CapacidadMaximaPool` permite crecimiento descontrolado de memoria (falla la aserción de saturación en 32).
- Restitución física en `Reiniciar`: retirar la llamada a `ArrayPool<T>.Shared.Return` en las ranuras sucias de `Reiniciar` rompe la simetría de buffers (`devueltosDelta < rentadosDelta`).
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

## 6. Coste de validar en `Current`

`Current` valida el token **solo** en `ValueLINQChunkDelay`, no en `ValueLINQSessionEnumerator`. La asimetría se decidió midiendo.

| Enumerador | `Current` se invoca | Coste de validar | Decisión |
|---|---|---|---|
| `ValueLINQSessionEnumerator` | por elemento | de 2.87 ns a 5.26 ns por elemento, **+83% (medido)** | no valida |
| `ValueLINQChunkDelay` | por fragmento | ratio 1.01 con fragmentos de 2 y 16, y 0.94 con 256 **(medido)** | valida |

**Entorno**: BenchmarkDotNet 0.15.8, job `net9.0`, Release, AMD Ryzen 9 3950X, Windows 11 Pro, 64 GB, SDK 10.0.301. Se usó un gemelo temporal de cada enumerador sin la comprobación para compararlos en la misma corrida; ambos gemelos se eliminaron después.

**Derivación**. En el caso de sesión el ratio converge a ~1.85 al crecer el tamaño (1.57 con 100 elementos, 1.77 con 500, 1.92 con 1 000, 1.87 con 10 000 y 1.83 con 100 000), que es lo esperable cuando el coste fijo se amortiza y solo queda el coste por elemento. La pendiente entre 100 y 1 000 da +2.72 ns por elemento y la de 100 000 da +2.39 ns: dos derivaciones independientes de los mismos datos.

En `Chunk` la diferencia queda **por debajo del suelo de ruido**. El 0.94 con fragmentos de 256 sale a favor de la variante validada, lo que no debe leerse como una mejora sino como que el efecto no es medible a esa escala.

---

## 7. Coste de las arenas en ejecución y amortización del reciclaje

Medido con `ValueLINQArenaBenchmarks` (consultas, `Size` 100 y 1000) y `ValueLINQArenaLifecycleBenchmarks` (ciclo de vida) en los seis jobs JIT y NativeAOT de net8/9/10, complementado con las mediciones de asignación de `ValueLINQArenaPoolingTests`. Las filas del motor perezoso solo existen en net9+; en net8 lanzan `PlatformNotSupportedException` y se reportan como NA, igual que en el resto de la suite.

| Qué | Resultado | Cifras de referencia (Size=100) |
|---|---|---|
| Enrutado por arena reutilizada, motor eager | no medible; la variante en arena sale entre el 0% y el 5% mejor en los 6 jobs, dentro del ruido | 424.6 vs 439.0 ns en net10; 435.0 vs 442.7 en NativeAOT 10 **(medido)** |
| Transporte de opciones de arena, motor perezoso | no medible | 180.8 vs 180.6 ns en net10; 190.6 vs 190.4 en NativeAOT 10 **(medido)** |
| `Chunk` reservando su buffer en la arena | no medible; la variante en arena sale un 1–2% mejor en los 6 jobs, dentro del ruido | 334.9 vs 338.6 ns en net10 **(medido)** |
| Alquilar y liberar una arena, un hilo | 53–62 ns, 0 B | los seis runtimes **(medido)** |
| Alquilar y liberar bajo 8 hilos simultáneos | 1.10–1.23 µs por operación (×18–22 sobre el caso aislado), 0 B | `OperationsPerInvoke` de 80 000; el alta y el join de los hilos quedan amortizados por debajo del 1% **(medido)** |
| Arena transitoria en frío (primer estreno de tipo en la arena) | 2.2–4.1 µs y 25 792 B | la materialización inicial de la tabla `(int, arena)` en cada estreno de identificador domina el coste **(medido)** |
| Arena transitoria en caliente (régimen estacionario con pool de tablas recicladas) | ~30–50 ns y **0 B en heap** | reutilización mediante `TryDequeue` + `Reiniciar` acotada a 32 tablas en `ValueLINQStateManager<T>`, erradicando las asignaciones en heap tras el calentamiento inicial **(medido con GC.GetAllocatedBytesForCurrentThread())** |

**Entorno de benchmarks BDN**: BenchmarkDotNet 0.15.8, Release, AMD Ryzen 9 3950X, Windows 11 Pro, 64 GB, SDK 10.0.301; jobs .NET 8.0/9.0/10.0 y NativeAOT 8.0/9.0/10.0.

**Entorno de pruebas de asignación cero**: `dotnet test -c Release -f net10.0`, ejecución secuencial bajo `AllocationAssert` y suites extendidas de 50 a 150 ciclos continuos.

### Por qué las cifras de Size=1000 del JIT no se citan

Con 1 000 elementos, las parejas ambiente/arena del JIT oscilan hasta ±20% **cambiando de dirección entre tandas y entre runtimes**: net9 pasó de +6% a −17% entre dos corridas consecutivas y net8 de +1% a +16%, mientras que en NativeAOT —con disposición de código fija— las mismas parejas quedan clavadas (1 122 vs 1 109 ns en AOT 10). Un coste sistemático de enrutado no cambia de signo entre tandas; la alineación de código y buffers del JIT sí. La igualdad en AOT actúa de experimento de control, así que ninguna cifra de Size=1000 del JIT debe citarse como coste del enrutado.

---

## 8. Sin medir

- **Tamaño de `ValueLINQDelayOptions`**, que pasó de 8 a 16 bytes al añadir las banderas. Se copia una vez por operador, no por elemento, así que son decenas de bytes por consulta; **razonado por estructura, no medido**.
- **Coste del barrido del reaper** y del sellado de vaciado en `TablaSesiones.PushIndice`/`PopIndice`, que ocurre dentro de una sección crítica ya existente en el camino de alquiler y liberación de sesiones.
- **Las cifras de memoria residente agregada** del sistema de arenas cuando todas las tablas posibles están materializadas: las de `Arenas.md` §5 son derivadas por fórmula. En pruebas se midió tanto la asignación de 25 792 B al estrenar una tabla en frío como la asignación de 0 B en régimen estacionario con reciclaje (§7).

---

## 9. Sin cubrir

- **El camino positivo del reintento de `Alquilar` tras agotamiento**: exigiría un segundo test de agotamiento de capacidad que competiría por identificadores con el que ya existe y volvería intermitentes a ambos.
- **El reciclaje real del ciclo completo de 4 095 identificadores de arena en una sola prueba de integración**: la distinción y rechazo de generación obsoleta y las versiones monotónicas por ranura sí están probadas mediante mutación forzada y pruebas unitarias (§4.3 y §5), pero agotar los 4 095 identificadores en un único test requeriría un tiempo de ejecución prolongado incompatible con la suite estándar.

---
[Volver a Arenas de Memoria](Arenas.md)
