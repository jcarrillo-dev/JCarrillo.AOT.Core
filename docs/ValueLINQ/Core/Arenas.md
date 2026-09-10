[Volver al Núcleo de ValueLINQ](README.md)

# Arenas de Memoria en ValueLINQ

Una **arena** es un ámbito de memoria explícito para las consultas de ValueLINQ. Todas las sesiones creadas dentro de una arena —incluidas las sesiones intermedias que generan los operadores— se liberan **en bloque** al disponer la arena, aunque el código cliente olvide liberar consultas individuales.

El modelo es el de las *regiones* / *arenas* clásicas (equivalente conceptual a `MemoryContext` de PostgreSQL o al ámbito *scoped* de la inyección de dependencias): se paga la reserva una vez, se usa sin ceremonia dentro del ámbito, y se recupera todo de golpe al cerrar.

> [!NOTE]
> **Estado**: funcionalidad implementada y cubierta por la batería de pruebas automatizadas: creación, disposición en bloque, aislamiento entre arenas, carreras de concurrencia, sincronización Reaper-Sesión, pool acotado de reciclaje de tablas con garantía de **0 B de asignación en heap en estado estacionario (medido con `GC.GetAllocatedBytesForCurrentThread()`)**, propagación por los operadores de **ambos** motores, recolección automática de arenas vacías con guarda atómica anti-reentrada y reglas de mezcla. El detalle de qué está verificado por mutación y contramuestra figura en [Verificación del sistema de arenas](Arenas.Verificacion.md). Las cifras de memoria física y balances de buffers están respaldadas por diagnósticos de `ArrayPoolEventSource`.

---

## 1. La Arena Ambiente (Arena 0)

El sistema reserva la **arena `0`** como **ambiente, interna y persistente**:

*   **Ambiente**: es la arena que se usa por defecto cuando una consulta se crea **sin** especificar arena. `datos.ToValueQuery()` (sin argumento de arena) crea su sesión en la arena 0. Todo el comportamiento previo de ValueLINQ es, en la práctica, "trabajar sobre la arena 0".
*   **Persistente**: la arena 0 se pre-alquila durante la inicialización del gestor de arenas (`ValueLINQArenaManager.cs:40-46`) y está marcada como persistente. **No puede liberarse**: cualquier intento de disponerla es un no-op seguro (`ValueLINQArenaManager.cs:103`), y jamás es candidata a la recolección automática por inactividad. Vive durante todo el proceso.
*   **Interna**: el usuario no la crea ni la gestiona. No existe un handle público hacia la arena 0; simplemente es el destino implícito de las consultas sin arena.

En consecuencia, **usar ValueLINQ sin arenas explícitas sigue funcionando exactamente igual que antes**: se opera sobre la arena 0, que nunca desaparece. Las arenas explícitas son una capacidad *opcional* que se añade encima, no un requisito.

---

## 2. Uso de Arenas Explícitas

El handle público es `ValueLINQArena`, un `readonly struct` inmutable de 8 bytes (`ValueLINQArena.cs:15`) que solo transporta un token de arena empaquetado (identificador de 12 bits + generación de 52 bits); el almacenamiento real vive en las tablas de sesión por tipo del `ValueLINQStateManager<T>`.

```csharp
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;

// La arena vive lo que dura el bloque `using`.
using ValueLINQArena arena = ValueLINQArena.Crear();

PooledArray<int> resultado = datos
    .ToValueQuery(arena)                        // sesión de entrada en la arena
    .Where(3, new MayorQue())                   // el destino de Where hereda la arena
    .Select<int, Doble, int>(new Doble())       // idem, incluso cambiando de tipo
    .ToArray();

// Al salir del `using`: toda sesión creada dentro de la arena se libera,
// se hubiera dispuesto individualmente o no.
```

### API pública de `ValueLINQArena`

| Miembro | Descripción |
|---|---|
| `static ValueLINQArena Crear(bool persistente = false, TimeSpan? inactividad = null)` | Alquila una arena nueva (`ValueLINQArena.cs:63`). `persistente` la excluye de la recolección automática; véase «Cómo debe crearse la arena» más abajo. `inactividad` fija cuánto debe permanecer vacía antes de que la recolección la libere, y si se omite se usa `ValueLINQConfig.TiempoInactividadArena` (5 minutos). No tiene efecto sobre una arena persistente. |
| `void Dispose()` | Libera la arena y **todas** las sesiones de ValueLINQ creadas dentro de ella, en cualquier tipo `T` (`ValueLINQArena.cs:70`). |
| `bool IsViva` | Indica si la arena sigue activa (no liberada ni recolectada) consultando `ValueLINQArenaManager.IsArenaViva(TokenArena)` (`ValueLINQArena.cs:34`). |
| `int Id { get; }` | Propiedad pública de solo lectura que expone el identificador numérico de la arena (de 0 a 4095) para telemetría, logs estructurados y diagnósticos (`ValueLINQArena.cs:25`), extraído directamente del token sin allocations vía `TokenHelper.ObtenerIdTokenArena(TokenArena)`. |
| `bool Equals(ValueLINQArena other)` | Implementación de `IEquatable<ValueLINQArena>` (`ValueLINQArena.cs:79`). Compara directamente los tokens `TokenArena` de 64 bits en registros de CPU sin overhead ni indirecciones. |
| `bool Equals(object? obj)` | Sobrescritura no encajonante (`obj is ValueLINQArena other && Equals(other)`) que previene boxing en el heap (`ValueLINQArena.cs:88`). |
| `int GetHashCode()` | Retorna `TokenArena.GetHashCode()` (`ValueLINQArena.cs:96`), garantizando coherencia matemática con la igualdad estructural y distribución uniforme en diccionarios y conjuntos hash. |
| `operator ==` / `operator !=` | Sobrecarga estricta de operadores de igualdad y desigualdad por valor entre handles de arena (`ValueLINQArena.cs:106-116`). |

> [!IMPORTANT]
> **La disposición de arenas explícitas sigue siendo obligatoria.** La recolección automática solo alcanza a las arenas no persistentes que quedan **vacías**: una arena olvidada que conserve una sola sesión viva retiene su identificador hasta el fin del proceso, y agotar los ~4095 disponibles hace fallar la creación de nuevas arenas. Usa `using` o `Dispose` explícito; considera la arena ambiente (arena 0, sin argumento) si no necesitas un ámbito propio.

### Cómo debe crearse la arena

Una arena se crea a mano y su handle se guarda en una variable o en un campo, a diferencia de una consulta, que puede quedarse a medio recorrer por accidente. Perder la referencia de una arena no es un descuido de enumeración: es no disponer un recurso que se tomó explícitamente. Por eso agotar los identificadores lanza en vez de degradarse en silencio — el fallo aparece donde alguien está creando arenas sin control.

Lo que determina cómo debe crearse **no es dónde vive el handle**, sino una sola pregunta:

> **¿Puede esta arena quedarse vacía más tiempo que su umbral y seguir haciendo falta?**

Que el handle viva en un campo es la forma más común de que la respuesta sea «sí», pero no la única: un bloque `using` que espere una operación lenta entre dos consultas deja la arena vacía exactamente igual, y el barrido no distingue un caso del otro.

| Situación | Creación | Liberación |
|---|---|---|
| Uso continuo dentro del bloque | `ValueLINQArena.Crear()` | Automática al salir del bloque. Candidata a la recolección automática. |
| Bloque con pausas largas conocidas (E/S, lote remoto, espera de terceros) | `ValueLINQArena.Crear(inactividad: …)`, con un margen mayor que la pausa más larga esperada | Automática al salir del bloque. Recuperable si se olvida, pero no antes del margen fijado. |
| **El handle sobrevive al bloque** (campo, servicio, objeto de sesión) | **`ValueLINQArena.Crear(persistente: true)`** | **Solo manual y obligatoria en todo camino (incluidas excepciones).** Renuncia a la red de seguridad del reaper: `RecolectarArenas` descarta las persistentes en su primera comprobación (antes de mirar si están vacías), así que una persistente olvidada no se recupera nunca, ni vacía ni por el barrido que dispara `Alquilar` bajo presión. Si un objeto de sesión se fuga en un camino de error, su identificador se pierde para el resto del proceso (solo hay ~4095). |

El umbral se cuenta desde que la arena se quedó **vacía**, no desde que se creó ni desde la última consulta; si ningún tipo llegó a materializar tabla, desde el alquiler. El margen de la segunda fila tiene que cubrir la pausa medida desde ese instante, que en una arena creada y aún sin usar es el propio `Crear`.

> [!NOTE]
> **`Crear` no valida `inactividad`.** No existe un suelo análogo al minuto que `TiempoLimpieza` impone a la limpieza de sesiones: el valor viaja tal cual hasta el estado de la arena. `TimeSpan.Zero` o un valor negativo la dejan recolectable en el primer barrido posterior a su vaciado, y un valor desmedido la deja fuera del alcance del reaper también cuando se agotan los identificadores, que es justo cuando interesa recuperarla.

#### `persistente` e `inactividad` no son intercambiables

`inactividad` **retrasa** el barrido; `persistente` **lo apaga** para esa arena. Para un bloque con una pausa larga lo correcto es `inactividad`: la arena sigue siendo recuperable si el bloque termina sin disponerla, solo que no antes del margen fijado. `persistente` ahí se pasa de fuerte, porque renuncia a esa red de seguridad durante toda la vida del proceso a cambio de nada que la pausa necesite.

Tampoco se suman: sobre una arena persistente el umbral no tiene efecto, porque el filtro de persistencia decide antes de que se mire el reloj.

> [!WARNING]
> **Toda arena que pueda quedarse vacía más tiempo que su umbral y seguir haciendo falta tiene que declararlo al crearse**: con `inactividad` si la espera está acotada y es conocida, con `persistente: true` si el handle sobrevive al bloque. El criterio del barrido es «vacía e inactiva durante el umbral», y ese criterio **no distingue** una arena olvidada de una arena viva que simplemente lleva un rato sin tráfico.
>
> El caso más común es el campo de larga duración: un servicio con una arena en un campo, correctamente gestionada, que pase el umbral entre dos ráfagas, vería su arena recolectada y la siguiente operación fallaría con `ValueLinqArenaInactivaException`. Pero un `using` con una espera larga en medio produce el mismo fallo por el mismo mecanismo, y ahí no hay ningún campo a la vista.
>
> Es el riesgo principal que introduce la recolección automática, y el más caro de diagnosticar: el fallo llega tarde, solo tras superar el umbral, y de forma intermitente.

#### Por qué el defecto no es persistente

La pregunta que deja abierta el aviso anterior es por qué `Crear()` no entrega una arena persistente y reserva la recolección a quien la pida explícitamente. Porque entonces el barrido no recogería nada en el único caso para el que existe.

El barrido está para quien **olvida** disponer la arena, y olvidar no es deliberado: nadie que se deje un `Dispose` ha pedido antes que le recojan la arena. Con un defecto persistente, el filtro de persistencia —la primera comprobación del barrido— descartaría precisamente todas las arenas olvidadas, y el mecanismo quedaría vivo solo para quien es lo bastante cuidadoso como para no necesitarlo.

Ese olvido hoy cuesta tiempo y nada más: las sesiones que quedaron dentro caducan por su propio umbral, la última en irse sella el vaciado, y solo entonces empieza a correr el umbral de la arena; en el peor caso, la suma de los dos (véase «Cuándo se recolecta una arena»). Después el identificador vuelve al FIFO y el proceso se ha curado solo. Con un defecto persistente ese identificador no volvería nunca: ni por el temporizador de fondo, ni por la recolección que `Alquilar` intenta antes de lanzar por capacidad agotada, porque respeta el mismo filtro. Agotar los ~4095 identificadores dejaría de ser un fallo recuperable para pasar a ser definitivo durante el resto de la vida del proceso.

Los dos defectos no fallan igual, y tampoco le fallan a la misma persona:

| | Defecto actual (no persistente) | Defecto persistente |
|---|---|---|
| Quién paga el error | quien no declaró un margen que necesitaba | quien olvidó un `Dispose` |
| Qué recibe | `ValueLinqArenaInactivaException`, con la arena identificada y su ficha [JCE0002](../../Diagnostico/JCE/JCE0002.md) | `InvalidOperationException` por capacidad agotada, en el hilo que pidió la última arena, que no suele ser el culpable |
| Cómo se recupera | declarando `inactividad` o `persistente` donde se crea la arena | no se recupera hasta reiniciar el proceso |

Nada de esto hace gratis el defecto elegido: es justamente lo que obliga a escribir el aviso anterior, y quien no declare su margen se llevará una excepción sobre código que por lo demás está bien. La elección es deliberada y se puede enunciar en una frase: se prefiere un fallo localizado, con nombre y con remedio en el sitio donde se crea la arena, antes que uno diferido, cobrado a un inocente y sin vuelta atrás.

### Cuándo se recolecta una arena

Una arena se libera automáticamente cuando cumple las tres condiciones a la vez:

1. **No es persistente.** La arena 0 y las creadas con `persistente: true` quedan fuera del barrido.
2. **Está vacía en todos los tipos.** Cada `ValueLINQStateManager<T>` informa de si conserva sesiones en esa arena; basta con que uno diga que sí para que la arena no sea candidata.
3. **Lleva vacía más que su propio umbral.** La referencia temporal es el instante en que se vació la **última** tabla, no la primera: mientras algún tipo siga ocupándola la arena no está vacía, y el reloj empieza cuando se vacía la última. Si ningún tipo llegó a materializar tabla —arena creada y nunca usada—, la referencia es el instante del alquiler.

El barrido corre en el mismo temporizador de fondo que la limpieza de sesiones (`ValueLINQGC`, cada 10 s) y también al agotarse la lista de identificadores libres: `Alquilar` intenta una recolección antes de lanzar, de modo que agotar la capacidad no es un fallo permanente si hay arenas recuperables.

En el peor caso una arena tarda en volver al pool lo que sumen los dos umbrales: primero sus sesiones deben expirar por inactividad y liberar las tablas, y solo entonces empieza a contar el umbral de la arena.

#### Invariantes de Concurrencia y Sincronización del Reaper

El proceso de recolección de fondo implementa dos invariantes de sincronización física estrictos para erradicar condiciones de carrera bajo alta concurrencia multihilo:

1. **Guarda Atómica Anti Spin-Storm (`_isRecolectando`)**:
   `ValueLINQArenaManager.RecolectarArenas()` (`ValueLINQArenaManager.cs:146-182`) está blindado mediante una bandera atómica de reentrada `_isRecolectando` (`ValueLINQArenaManager.cs:26`):
   ```csharp
   if (Interlocked.CompareExchange(ref _isRecolectando, 1, 0) != 0)
       return 0;
   try
   {
       // Barrido de arenas vacías...
   }
   finally
   {
       Volatile.Write(ref _isRecolectando, 0);
   }
   ```
   Si un hilo entra a ejecutar el barrido (por ejemplo, desde el temporizador periódico `ValueLINQGC`), cualquier solicitud concurrente proveniente de otro hilo (como la invocación bajo presión de capacidad en `Alquilar()` o llamadas manuales) detecta la contención inmediatamente y retorna `0` sin bloquearse ni provocar tormentas de espín (*spin-storms*) sobre los bloqueos de partición. La liberación en el bloque `finally` garantiza la restauración incondicional de la bandera incluso ante excepciones no controladas.

2. **Remediación de Carrera Reaper-Sesión (Protección contra Búferes Huérfanos en `ArrayPool`)**:
   En escenarios de concurrencia extrema, un hilo podría solicitar una nueva sesión en una arena (`TablaSesiones<T>.ObtenerMetadatos`) en el milisegundo exacto en que el Reaper de fondo decide recolectarla por inactividad. Sin sincronización, el hilo podría alquilar un arreglo en `ArrayPool<T>.Shared.Rent`, mientras el Reaper ya marcó la arena como inactiva, dejando el búfer rentado huérfano para siempre en el proceso.
   
   Para prevenir esta fuga, `TablaSesiones<T>.InicializarSlot` (`TablaSesiones.cs:203-220`) adquiere el `SpinLock` de ranura correspondiente y **revalida atómicamente la vitalidad y generación de la arena antes de alquilar el búfer**:
   ```csharp
   using ValueLINQSpinLock spinLock = new(ref _spinLocks[particion][index].Lock);

   if (_arenaId != 0 && (!ValueLINQArenaManager.IsArenaViva(_arenaId) || ValueLINQArenaManager.ObtenerGeneracion(_arenaId) != _arenaGen))
       ThrowArenaInactiva(_arenaId);

   // Solo si sigue viva se procede al alquiler físico:
   ref MetadatosSesion<T> metadato = ref _datos[particion][index];
   // ... cálculo de token ...
   InicializarMetadatos(ref metadato, token, tamañoMinimo); // Alquila ArrayPool
   ```
   Si la arena ya no está viva o su generación no coincide, se lanza `ValueLinqArenaInactivaException`. El bloque `catch` envolvente en `ObtenerMetadatos` (`TablaSesiones.cs:196-200`) captura la excepción y ejecuta inmediatamente `PushIndice(indice)`, devolviendo el índice de ranura a la pila libre y garantizando que ningún búfer sea alquilado en el `ArrayPool`.

3. **Cierre Seguro de Liberación Concurrente (`LiberarMetadatos`)**:
   De forma simétrica, cuando una sesión se libera (`TablaSesiones.cs:250-275`), el método adquiere el spinlock del slot, extrae el arreglo `arrayADevolver` y marca la ranura como dispuesta. Si la arena fue desactivada de forma concurrente, el búfer extraído se devuelve de manera incondicional a `ArrayPool<T>.Shared.Return(arrayADevolver, ...)` en la línea 273, garantizando un balance neto simétrico absoluto de búferes alquilados frente a devueltos (`BufferReturnedCount == BufferRentedCount`).

### Puntos de entrada con arena

Se ofrecen sobrecargas de los inicializadores que aceptan la arena de destino para arreglos, spans, memoria y colecciones pooled:

```csharp
// Arreglos nativos
public static ValueLINQStruct<T>    ToValueQuery<T>(this T[] origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this T[] origen, ValueLINQArena arena);

// Spans en pila
public static ValueLINQStruct<T>    ToValueQuery<T>(this Span<T> origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this Span<T> origen, ValueLINQArena arena);
public static ValueLINQStruct<T>    ToValueQuery<T>(this ReadOnlySpan<T> origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ReadOnlySpan<T> origen, ValueLINQArena arena);

// Memoria
public static ValueLINQStruct<T>    ToValueQuery<T>(this ref Memory<T> origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref Memory<T> origen, ValueLINQArena arena);

// Colecciones Pooled
public static ValueLINQStruct<T>    ToValueQuery<T>(this ref PooledList<T> origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref PooledList<T> origen, ValueLINQArena arena);
public static ValueLINQStruct<T>    ToValueQuery<T>(this ref PooledArray<T> origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref PooledArray<T> origen, ValueLINQArena arena);
```

Adicionalmente, `PooledArray<T>` cuenta con sobrecargas simétricas sin arena que operan sobre la arena ambiente 0:
```csharp
public static ValueLINQStruct<T>    ToValueQuery<T>(this ref PooledArray<T> origen);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref PooledArray<T> origen);
```

#### Blindaje de Aislamiento y Validación de Arena Activa
Todas las sobrecargas que reciben `ValueLINQArena` (`ValueLINQExtensions.cs:71-330`) validan inmediatamente que la arena permanezca activa:
```csharp
if (!arena.IsViva)
    ThrowArenaInactiva(arena.Id);
```
Esta guarda impide que una arena inválida, cerrada o un handle `default(ValueLINQArena)` degrade silenciosamente a la arena ambiente 0; ante cualquier handle inactivo se lanza inmediatamente `ValueLinqArenaInactivaException`. La arena ambiente 0 solo es accesible de manera deliberada a través de las sobrecargas sin parámetro de arena (`ToValueQuery()` y `ToValueRefQuery()`).

### Propagación por los operadores

Los operadores eager (`Where`, `Select`, `Chunk`, `Concat`) crean su sesión de destino **en la misma arena que su origen**, deduciéndola del token de la consulta de entrada. Esto garantiza que una cadena iniciada en una arena mantiene *todas* sus sesiones intermedias dentro de esa arena, de modo que la disposición en bloque las alcanza todas.

El aislamiento cruzado por tipo funciona porque la disposición de una arena se difunde a **todos** los tipos `T` que hayan registrado tablas en ella. Un `Select<int, …, string>` crea su destino en la tabla de `string` de esa arena, y disponer la arena también la libera.

> [!NOTE]
> **`Concat` crea su destino en la arena de su primer operando y no restringe la mezcla.** Los operandos pueden venir de arenas distintas: cada uno se copia al destino antes de retornar, así que solo necesitan estar vivos durante la llamada. La restricción de una sola arena explícita aplica únicamente al motor perezoso; véase «Una sola arena explícita donde la consulta almacena» más abajo.

### Una sola arena explícita donde la consulta almacena

Una consulta solo es utilizable mientras sigan vivas todas las arenas de las que dependen sus datos, de modo que su vida útil es la **intersección** de esas vidas. Con una sola arena explícita esa intersección es un hecho único y comprobable de una vez, y disponerla invalida la consulta **entera**, que es exactamente lo que pidió quien la cerró. Con varias, la consulta pasa a depender de ámbitos independientes: su ventana de validez deja de poder razonarse y, cuando una muere, no hay forma de saber si fue un descuido o una decisión. La restricción convierte esa situación en irrepresentable en lugar de en un caso a manejar.

**La arena ambiente queda fuera de la cuenta, y es deliberado.** No se puede liberar y vive hasta el fin del proceso (§1), así que sumarla no introduce una segunda vida útil que pueda expirar. Lo que se limita es la cantidad de arenas **explícitas**, que son las que sí mueren.

> [!IMPORTANT]
> **La restricción existe solo en el motor perezoso.** El eager **no restringe** la mezcla de arenas, y no es una laguna: cada operador copia sus operandos a una sesión destino **antes de retornar**, así que las arenas de origen solo tienen que estar vivas durante la llamada. Después, el resultado es una copia que ya no depende de ellas. Restringir ahí no protegería de nada: el único riesgo durante la copia —que otro hilo cierre una arena a media operación— existe idéntico con una sola arena, así que tener dos no añade exposición.
>
> La diferencia entre motores se resume en una frase: **el eager copia y suelta, el perezoso retiene**. El perezoso conserva los enumeradores de origen durante toda la enumeración, y por eso ahí sí hay una conjunción de vidas que gestionar.

#### Para qué sirve la excepción, y para qué no

La excepción existe por **ergonomía en el sitio de llamada**: permite añadir datos a una consulta sin tener que arrastrar la arena hasta cada operando. La consulta puede vivir en la arena X y los argumentos inicializarse por defecto, que es el caso habitual de `Concat`:

```csharp
using ValueLINQArena arena = ValueLINQArena.Crear();

using ValueLINQStruct<int> consultaExtra    = extra.ToValueQuery();      // ambiente
using ValueLINQStruct<int> consultaMasExtra = masExtra.ToValueQuery();   // ambiente

using ValueLINQStruct<int> consulta = datos
    .ToValueQuery(arena)                                    // la consulta vive en la arena
    .Concat(consultaExtra, consultaMasExtra);               // los operandos no repiten la arena
```

Sin esta excepción, cada operando tendría que crearse con la arena aunque el llamante no tenga por qué conocerla —pensemos en datos que llegan de otra capa—, o habría que renunciar a `Concat`.

> [!NOTE]
> Los operandos ambientes **no** los alcanza el `Dispose` de la arena, porque no pertenecen a ella: siguen necesitando su propio `using` o quedarán retenidos hasta que el limpiador de fondo los barra por inactividad. La arena solo libera en bloque lo que se creó dentro de ella, y el destino del `Concat` sí lo es.

> [!CAUTION]
> **No es un mecanismo para pasar datos de una arena a otra.** Mezclar no reubica nada: cada sesión sigue viviendo donde se creó, el destino se crea en la arena del primer operando, y las sesiones ambientes que entraron en la consulta siguen siendo ambientes. Para trasladar datos de verdad entre arenas hay que materializar y volver a entrar, que es una copia explícita y visible en el código: véase «Pasar datos de una arena a otra» más abajo.

La regla vive en `ValueLINQArenaManager.CombinarArena` (`ValueLINQArenaManager.cs:223-235`) y la consume `ValueLINQDelayOptions.ValidarArenaUnica`. Acumula la arena explícita según recorre los operandos en vez de comparar cada uno contra el destino: de lo contrario una canalización que arranca en la arena ambiente admitiría operandos de arenas distintas entre sí. Una consulta *default* tiene identificador de arena cero, así que tampoco impone ámbito.

#### Qué protege exactamente la restricción

La regla se enuncia sobre la arena en la que la consulta **almacena estado**, que es siempre la del receptor:

> Una consulta creada en una arena explícita admite la arena ambiente como acompañante, y **nunca una segunda explícita**.

El motivo es que en esa arena vive **todo** lo que la consulta posee: sus sesiones de entrada, las intermedias de cada operador y el estado que va guardando mientras itera —el buffer de `Chunk`, y mañana el de `Order` o `Join`—. Admitir una segunda explícita haría que ese conjunto dependiese de dos vidas ajenas. De ahí que la restricción no sea negociable.

##### La razón de fondo: C# no sabe expresar esta vida útil

La restricción no nace del miedo a un fallo concreto, sino de una **carencia del lenguaje**: C# no tiene forma de declarar que un valor depende de la vida de otro. No existe manera de escribir «esta consulta requiere que X e Y sigan vivas», ni de que el compilador lo verifique, ni de que impida cerrar una de las dos antes de tiempo.

En un lenguaje con propiedad y vidas útiles en el sistema de tipos —Rust es el ejemplo obvio— esto no sería una regla de librería: se expresaría con parámetros de vida y el compilador rechazaría el programa mal formado. Ahí una consulta podría depender de varias arenas sin problema, porque la garantía existiría.

Aquí no existe, y de ahí la postura: **ValueLINQ se niega a representar un estado cuya validez no puede garantizar**. No se trata de que mezclar arenas sea intrínsecamente incorrecto, sino de que no hay ningún mecanismo con el que sostener la promesa. Antes que ofrecer una capacidad cuya corrección quede enteramente en manos de la disciplina del consumidor, se restringe el modelo a lo que sí es comprobable.

Una arena es una vida útil que **la consulta no controla**. Quien la creó puede disponerla en cualquier momento, y hace bien: para eso es un ámbito explícito.

Con una sola arena explícita, la validez de la consulta se enuncia como un hecho único: *«vale mientras viva X»*. Eso no solo es comprobable, es **poseíble**: existe un sitio concreto del código donde poner el `using`, y normalmente es el mismo que creó la consulta.

Y, sobre todo, disponerla es **una intención legible**. Quien la cierra está diciendo «ya no quiero más consultas en este ámbito», y lo que ocurre es exactamente eso: la consulta deja de valer **entera**, con todos sus datos, no solo con el fragmento que estuviera construyendo. No hay pérdida parcial que interpretar. Esa totalidad es lo que hace el fallo coherente: el efecto coincide con lo que se pidió.

Con dos, el enunciado pasa a ser *«vale mientras vivan X **e** Y»*, y el problema no es que sea más frágil, es que **esa conjunción no tiene dueño**. Si la capa A posee `arenaA` y la capa B posee `arenaB`, ninguna sabe de la existencia de la otra y no hay ningún ámbito en el programa que signifique «las dos a la vez». La primera que cierre estará comportándose correctamente, cumpliendo su propio contrato, y aun así romperá la consulta. No hay a quién atribuir la responsabilidad ni dónde colocar el `using` que la garantice.

Peor aún, cerrar una y no las otras **deja de ser interpretable**. ¿Fue un descuido? ¿Fue deliberado, y quien cerró consideraba que su parte ya no hacía falta? La intención no es algo que el sistema pueda determinar. Lo único determinable es que la consulta ha quedado inválida. Y un sistema que no puede distinguir la intención solo puede reportar, nunca decidir: de ahí que la restricción elimine la situación en lugar de intentar manejarla.

Que la propiedad se cumple está **comprobado y no solo razonado**: `UnaTuberiaCreadaEnUnaArenaConservaEsaArenaHastaElFinalDeLaCadena` y su equivalente eager fijan que una consulta creada en una arena termina la cadena —con filtros, proyecciones, concatenaciones con operandos ambientes y un operador que reserva— conservando el mismo identificador de arena. Verifican la propiedad final, no el rechazo del `Concat`, precisamente porque la restricción se apoya en que el almacenamiento siga al receptor y esa es una decisión tomada en otro lugar del código.

Por eso la arena ambiente no cuenta y no es un parche: no tiene vida útil que intersecar, así que `X ∧ ambiente = X`. Sumarla deja el enunciado igual. Es el elemento neutro de la conjunción, y es el mismo neutro —el cero— que usa `CombinarArena` en el código.

> [!WARNING]
> **El peligro concreto de permitir el salto libre entre arenas** no es recibir una excepción, sino no recibirla. Al liberarse una arena, los buffers de sus sesiones vuelven a `ArrayPool<T>.Shared`. Un operador que estuviera escribiendo estado en esa arena seguiría rellenando un arreglo que ya está en el pool y que otro consumidor sin relación puede alquilar acto seguido: dos dueños escribiendo sobre la misma memoria. Eso no es un fallo detectable, es *aliasing*, y el síntoma aparece lejos del `Dispose` que lo causó, más tarde y probablemente en otro hilo.
>
> Anclar la regla en **dónde se escribe** es lo que mantiene esa situación fuera del espacio de estados representables. Leer de varias arenas no tiene ese problema: si una muere, el enumerador que la lee lanza (§4) y el error llega identificado y a tiempo.

El comportamiento observable se sigue de eso:

```csharp
enArena1.Concat(ambiente)                     // permitido: la ambiente no expira
enArena1.Concat(ambiente).Concat(enArena2)    // ValueLinqArenaCruzadaException (comprobado)
ambiente.Concat(enArena1).Concat(enArena2)    // permitido (comprobado)
```

La tercera forma parece una fuga de la regla y no lo es: la consulta nace ambiente, así que **almacena en la ambiente** y no tiene estado propio en ninguna arena explícita que pueda perderse. `enArena1` y `enArena2` intervienen solo como orígenes de lectura y, si alguno muere, su enumerador lanza (§4) en vez de truncar. Lo que la restricción protege es la **escritura**, y ahí la respuesta sigue siendo única: la arena del receptor.

El motor eager no llega a plantear esa forma, porque cada `Concat` materializa un destino único y el encadenamiento colapsa en una sola sesión en cada paso.

#### La escotilla: `SinComprobarLimitesDeArena`

Quien **sabe** que todas las arenas implicadas siguen vivas durante una consulta puntual puede renunciar al recuento:

```csharp
using JCarrillo.AOT.Core.ValueLINQ.Marshalling;

var consulta = datos.ToValueDelayQuery(arenaA)
                    .SinComprobarLimitesDeArena()
                    .Concat(otros.ToValueDelayQuery(arenaB));
```

Vive en `JCarrillo.AOT.Core.ValueLINQ.Marshalling`, junto al resto de renuncias explícitas del motor perezoso. El namespace es **organización, no barrera**: el filtro real lo hacen el nombre del método y su contrato. Esconder la capacidad no filtraría por competencia sino por diligencia leyendo documentación, y dejaría sin enterarse precisamente a quien la necesita; por eso `ValueLinqArenaCruzadaException` remite desde su propio mensaje a [JCE0001](../../Diagnostico/JCE/JCE0001.md), donde se explica primero cómo evitar el problema y solo después cómo asumirlo.

**Qué se compromete a garantizar quien la usa**: que todas las arenas implicadas siguen vivas hasta que la enumeración termine o la consulta se materialice. Si alguna se libera antes, el enumerador que la lea lanza; la promesa es verificable en ejecución y no un acto de fe.

**Qué apaga**: únicamente el recuento de arenas explícitas entre los operandos. **Qué sigue intacto**: los operadores que reservan lo siguen haciendo en la arena de la canalización y comprueban que vive, la enumeración sigue validando la sesión, y la arena de almacenamiento sigue sin poder reasignarse.

**Alcance**: es una propiedad de la canalización, no de la llamada. No significa que ese `Concat` no compruebe, sino que **de ahí en adelante** esa consulta no comprueba. No hay forma de reactivarlo, y es deliberado: reactivar no revalidaría lo ya compuesto, así que un API simétrico daría confianza falsa.

#### Por qué leer tolera varias arenas y escribir no

La distinción que hace principiada esta tolerancia es que **mezclar solo afecta a los orígenes, nunca al destino**.

Leer admite pluralidad porque la pregunta se responde por enumerador: cada uno valida su propio token de sesión, así que si su arena murió, ese enumerador **lanza** (§4). No hace falta un criterio global; cada fuente sabe dónde mirar y responde por sí misma, también al fallar. Esto habilita el caso que motiva dejarlo abierto: **componer entre capas**. Cada capa puede tener su propia arena y aportar sus datos a una consulta sin que ninguna necesite conocer la arena de las demás, con la arena ambiente actuando de punto de encuentro.

Escribir no admite pluralidad porque la pregunta pasa a ser única: un operador que **reserva** —hoy `Chunk`, mañana `Order` o `Join`— necesita una sola respuesta a «¿de qué arena es este buffer?». Con varias arenas explícitas entre los operandos del mismo paso no hay respuesta no arbitraria, y el comparador tendría que contrastar contra N arenas, siendo N el número de parámetros de ese paso, sin criterio alguno para elegir ganadora. Por eso el destino sigue al receptor y por eso un paso se limita a una sola arena explícita: mantiene anclada la escritura.

> [!NOTE]
> Que la lectura se resuelva por enumerador **no significa que una arena muerta pase inadvertida**: el enumerador afectado lanza en lugar de detenerse (§4), de modo que componer entre capas con arenas de vidas distintas produce un error explícito y no un resultado truncado que aparente estar completo. La pluralidad de orígenes es admisible precisamente porque cada uno responde por sí mismo, incluido al fallar.

### Propagación en el motor perezoso (Delay)

El motor perezoso transporta el token de arena en `ValueLINQDelayStruct._opciones` (`ValueLINQDelayStruct.cs:20`), un `ValueLINQDelayOptions` que los operadores copian de la canalización de origen a la de destino. Se guarda el token completo —identificador y generación— y no solo el identificador, porque los identificadores se reciclan y solo el token entero distingue encarnaciones (§3).

Puntos de entrada:

```csharp
public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this T[] origen,            ValueLINQArena arena);
public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this Span<T> origen,        ValueLINQArena arena);
public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this ReadOnlySpan<T> span,  ValueLINQArena arena);
```

Declaradas en `ValueLINQDelayExtensions.cs:102`, `:119` y `:133`. Las sobrecargas sin arena siguen creando la canalización en la arena ambiente.

Cuando la canalización perezosa nace de una consulta eager —`Delay()` o `ToValueDelayQuery(consulta)`— **hereda la arena de esa sesión** (`ValueLINQExtensions.cs:1639`, `:1652`). La reconstrucción del token de arena a partir del token de sesión la hace `ValueLINQArenaManager.TokenArenaDesdeSesion` (`ValueLINQArenaManager.cs:188-201`): el token de sesión solo guarda los bits bajos de `arena_gen`, así que se contrastan contra la generación viva y, si no cuadran, la arena se recicló entre medias y se lanza `ValueLinqArenaInactivaException`.

El único operador perezoso que reserva memoria hoy es `Chunk`, que enruta su buffer a la arena de la canalización (`ValueLINQChunkDelay.cs:134`). Si la arena ya fue liberada lanza `ValueLinqArenaInactivaException` en vez de recaer en la arena ambiente: recaer convertiría un error de vida de objetos en una fuga silenciosa. `Where`, `Select` y `Concat` no reservan sesiones y se limitan a propagar el token.

> [!IMPORTANT]
> **Quién decide el almacenamiento es el receptor**, no la arena «más específica». `x.ToValueDelayQuery().Concat(a.ToValueDelayQuery(arena), b.ToValueDelayQuery(arena))` es válida y almacena en la arena ambiente: el receptor define el flujo y los argumentos son datos que entran en él. Que un argumento cambiase dónde almacena el flujo sería configuración a distancia; si se quiere ámbito de arena, se pide al iniciar la cadena. Es la misma regla que aplica el motor eager a su primer operando.
>
> La restricción de una sola arena explícita se comparte con el motor eager y se detalla más arriba; en el perezoso la aplica `ValueLINQDelayOptions.ValidarArenaUnica` (`ValueLINQDelayOptions.cs:111`, `:118`, `:126`).

### Pasar datos de una arena a otra

No existe —ni se planea— un operador que mueva sesiones entre arenas de forma implícita. Cruzar de una arena a otra es **copiar datos**, y el diseño exige que esa copia sea visible en el sitio de llamada en vez de quedar escondida dentro de un operador.

El camino consiste en materializar la consulta y volver a entrar sobre la arena destino:

```csharp
using ValueLINQArena origen  = ValueLINQArena.Crear();
using ValueLINQArena destino = ValueLINQArena.Crear();

// 1. Materializar fuera de toda arena: el intermedio vive en el ArrayPool.
using PooledArray<int> materializado = datos
    .ToValueDelayQuery(origen)
    .Where<MayorQue, int>(3)
    .ToArray();

// 2. Volver a entrar en consulta sobre la arena destino.
var enDestino = materializado.Span.ToValueDelayQuery(destino);
```

El buffer intermedio se pide al `ArrayPool` y **no pertenece a ninguna de las dos arenas**, de modo que disponer cualquiera de ellas no lo invalida. Los materializadores del motor perezoso (`ToList`, `ToArray`, `ToListStandard`, `ToArrayStandard`) están en `ValueLINQDelayMaterializerExtensions` y disponen el enumerador de origen en un `finally`. El motor eager expone `ToArray` / `ToArrayRef` equivalentes (`ValueLINQExtensions.cs:1409`, `:1543`).

> [!NOTE]
> El paso 1 recorre el flujo completo y copia sus elementos: es la única forma de romper la dependencia con la arena de origen. Coste no medido con BenchmarkDotNet; es una copia lineal en el número de elementos **(estimado por construcción del algoritmo)**.

---

## 3. Estructura del Token y Aislamiento entre Encarnaciones

El empaquetado de bits en los tokens de ValueLINQ resuelve la identificación física, la pertenencia a arena y la protección contra condiciones ABA en una única palabra de 64 bits (`long`), manipulada mediante primitivas atómicas de hardware (`TokenHelper.cs:1-154`).

### Distribución de Bits del Token de Sesión (64 bits)

```
[ version (28 bits) | arena_gen (12 bits) | arena_id (12 bits) | slot (12 bits) ]
63                36 35                 24 23                12 11              0
```

*   **`slot`** (bits 0 a 11, 12 bits, máscara `0xFFF`): índice físico de la ranura de sesión dentro de la tabla (hasta 4096 ranuras). En la arquitectura particionada de `TablaSesiones<T>`, el slot se descompone internamente en `particion = slot >> 6` (6 bits, 64 particiones) y `offset = slot & 0x3F` (6 bits, 64 ranuras por partición).
*   **`arena_id`** (bits 12 a 23, 12 bits, máscara `0xFFF`): identificador numérico de la arena a la que pertenece la sesión (hasta 4096 arenas simultáneas, incluida la arena ambiente 0). Permite enrutar cualquier operación en $O(1)$ directo hacia `ValueLINQStateManager<T>._tablas[arena_id]` sin búsquedas ni diccionarios.
*   **`arena_gen`** (bits 24 a 35, 12 bits, máscara `0xFFF`): generación de la arena. Como los identificadores de arena se reciclan a través de una cola circular FIFO (`ValueLINQArenaManager._idsLibres`, `ValueLINQArenaManager.cs:33-38`), la generación desambigua **encarnaciones distintas del mismo identificador**: una sesión residual perteneciente a una arena ya liberada no puede colisionar con una sesión de una arena nueva que haya reutilizado ese mismo identificador.
*   **`version`** (bits 36 a 63, 28 bits): contador monótonamente creciente por ranura (`++metadato.Version`, `TablaSesiones.cs:214`). Proporciona protección ABA intra-encarnación para sesiones recicladas dentro de la misma arena.

### Distribución de Bits del Token de Arena (64 bits)

El handle `ValueLINQArena` transporta internamente su propio `TokenArena` de 64 bits (`TokenHelper.cs:109-111`):
```
[ arena_gen (52 bits) | arena_id (12 bits) ]
63                  12 11                 0
```
Esta representación unificada permite comparar handles de arena de forma ultra-rápida (`TokenArena == other.TokenArena`) sin encajonamiento en heap y comprobando tanto el identificador como la encarnación exacta en una sola instrucción de máquina.

### Doble Protección Estricta contra Colisiones ABA

El sistema implementa dos barreras ortogonales e independientes contra el problema ABA:

1. **Inmunidad Intra-Encarnación (Monotonicidad de `metadato.Version`)**:
   Cada vez que una ranura se libera y vuelve a ser asignada dentro de la misma tabla de sesiones, `TablaSesiones<T>.InicializarSlot` (`TablaSesiones.cs:214`) incrementa estrictamente `++metadato.Version`. El espacio de 28 bits permite más de **268 millones de reciclajes por ranura ($2^{28} = 268\,435\,456$)** antes de que el contador dé una vuelta completa. Si un hilo retiene un token caducado e intenta acceder a la ranura, `TokenHelper.LeerToken(ref metadatoRef.Token) != token` detecta la discrepancia de versión y lanza inmediatamente `ValueLinqSesionExpiradaException`.

2. **Inmunidad Inter-Encarnación (Generación de Arena y Reciclaje FIFO)**:
   Cuando una arena se dispone (`ValueLINQArenaManager.Liberar`), su identificador no se reutiliza de inmediato; se encola al final del array circular FIFO (`_idsLibres`). Para que un identificador vuelva a emitirse, deben haberse alquilado y consumido los restantes ~4094 identificadores del pool. Cuando finalmente se reasigna en `HasAlquilado` (`ValueLINQArenaManager.cs:87`), su generación se incrementa atómicamente (`++estado.Generacion`). Cualquier consulta, token de sesión o handle antiguo que intente interactuar con la tabla reciclada es rechazado de inmediato porque su `arena_gen` no coincide con la generación activa de la arena (`ValueLINQStateManager.cs:128`, `:161`, `:227`).

> [!NOTE]
> **El valor `0L` está reservado como token centinela nulo** y jamás identifica una sesión válida: es el token de una consulta *default* y la marca interna de ranura vacía o dispuesta en `MetadatosSesion<T>`. El generador de tokens lo garantiza activamente: en `TablaSesiones<T>.InicializarSlot` (`TablaSesiones.cs:212-215`), el cálculo del token se ejecuta dentro de un bucle `do { ... } while (token == 0L);`. Si por desbordamiento de la versión o coincidencia modular de bits se produjera `0L`, la versión se vuelve a incrementar automáticamente. Así, ninguna sesión activa recibe jamás el token cero.

---

## 4. Contrato de Vida Útil

> [!IMPORTANT]
> **Una sesión solo es válida mientras su arena esté viva.** Usar una consulta (o un token de sesión) después de disponer su arena es *comportamiento indefinido*, análogo a un *use-after-free* de un ámbito. Es el mismo contrato que el de cualquier sistema de arenas/regiones (en Rust lo impone el compilador; aquí se documenta).

El sistema **falla de forma segura y ruidosa** ante esta clase de uso: gracias a la validación por token y a la generación de arena, una sesión de una arena dispuesta se detecta como inválida y se lanza `ValueLinqArenaInactivaException` o `ValueLinqSesionExpiradaException`, según la arena siga viva o no. No hay corrupción silenciosa de datos ni resultados parciales.

Esto incluye la **enumeración perezosa**: perder la sesión durante el recorrido lanza desde `MoveNext` en vez de terminar la enumeración devolviendo `false`. Detenerse entregaría los elementos ya leídos y produciría un resultado truncado indistinguible de uno completo, que es un dato incorrecto y no un fallo: se propaga a cualquier conteo, suma o comprobación de «¿los procesé todos?» sin una sola señal. Es el mismo criterio que ya aplicaba el motor eager, cuyo `GetEnumerator` valida el token al abrir, y tiene precedente en la propia BCL, donde el enumerador de `List<T>` lanza a mitad del recorrido si la colección se modifica.

Las dos consecuencias visibles son que **reenumerar una consulta perezosa ya consumida lanza** en lugar de entregar cero elementos —la primera enumeración libera la sesión al agotarla—, y que una arena recolectada por inactividad lo comunica en vez de esconderlo, que es justamente el diagnóstico que indica que a esa arena le faltaba `persistente`.

#### Dónde se valida y dónde no

La comprobación vive en `MoveNext` de ambos enumeradores perezosos, y en `Current` **solo** en `ValueLINQChunkDelay`.

Lo que eso significa para tu código: si retienes la referencia que devuelve `ValueLINQSessionEnumerator.Current` **más allá de la iteración** y la lees después, esa lectura no está validada. Es de solo lectura sobre un `ReadOnlySpan<T>`, así que no puedes corromper nada, pero podrías leer un dato ya reciclado. Dentro del bucle no hay ventana: el `MoveNext` inmediatamente anterior ya validó.

En `Chunk` no existe esa salvedad, porque su `Current` entrega un `ReadOnlySpan` sobre el buffer rentado que el consumidor sostiene durante todo el cuerpo del bucle, y ahí sí se valida en cada acceso.

La asimetría se decidió midiendo: validar por elemento encarece el recorrido un 83%, y validar por fragmento no es medible. Las cifras y el entorno están en [Verificación del sistema de arenas](Arenas.Verificacion.md).

---

## 5. Coste de Memoria y Arquitectura del Pool de Tablas Recicladas

### La Penalización de Entrada Original vs Régimen Estacionario

En el diseño sin reciclaje de tablas, la primera sesión de un tipo `T` en una arena requería instanciar una nueva `TablaSesiones<T>` y materializar sus particiones bajo demanda. Para `T = int`, la asignación física medida en arranque en frío es de **25 792 B por estreno de `(int, arena)` (medido)**, correspondiente a la tabla, el stack de ranuras y los arreglos de metadatos y spinlocks de partición. Si una aplicación crea y destruye arenas de corta duración de manera iterativa (por ejemplo, por cada petición HTTP o mensaje recibido), esta reserva repetitiva presionaba innecesariamente al recolector de basura.

Para erradicar por completo estas asignaciones, `ValueLINQStateManager<T>` implementa un **pool acotado de reciclaje de tablas de sesión** (`ValueLINQStateManager.cs:16-17` y `TablaSesiones.cs:404-445`).

### Arquitectura de `TablaSesiones<T> Pooling`

```
                                  ValueLINQStateManager<T>
                                 ┌────────────────────────┐
   Alquilar Arena nueva          │  _tablas[id] (activo)  │          Dispose Arena
  ─────────────────────────────► │                        │ ─────────────────────────────►
   TryDequeue()                  └────────────────────────┘  Exchange(..., null)
        │                                                         │
        │                                                         ▼
        │                                                    LiberarTodo()
        │                                                         │ (devuelve buffers
        ▼                                                         │  a ArrayPool)
   ┌──────────────────────────────────────────────────────────┐   ▼
   │  _poolTablas: ConcurrentQueue<TablaSesiones<T>> (Cap: 32)│◄──┴─ si Count < 32: Enqueue()
   └──────────────────────────────────────────────────────────┘      si Count >= 32: GC Drop
        │
        ▼ (Tabla reciclada)
   Reiniciar(nuevoIdArena, nuevaArenaGen)
   - Reasigna _arenaId y _arenaGen
   - Reconstruye _indicesLibresStack (_topStack = 4096)
   - Limpia metadatos en particiones materializadas
   - 0 B heap allocations (medido)
```

1. **Cola Concurrente Acotada (`_poolTablas`)**:
   Cada especialización genérica de `ValueLINQStateManager<T>` mantiene una `ConcurrentQueue<TablaSesiones<T>>` estática con capacidad máxima delimitada a **32 tablas por tipo** (`CapacidadMaximaPool = 32`, `ValueLINQStateManager.cs:16`). Esta cota superior previene fugas de memoria no acotadas (*unbounded memory growth*) si se producen picos transitorios de miles de arenas simultáneas.

2. **Mecánica de Devolución (`LiberarTablasDeArena`)**:
   Al disponerse una arena (`ValueLINQStateManager.cs:195-207`), se desasocia atómicamente la tabla mediante `Interlocked.Exchange(ref _tablas[id], null)`. A continuación, se invoca `tabla.LiberarTodo()` (`TablaSesiones.cs:370-398`), devolviendo inmediatamente todos los arreglos alquilados a `ArrayPool<T>.Shared`. Si el número de instancias en `_poolTablas` es inferior a 32, la tabla desocupada se encola en el pool; si el pool está lleno, la tabla se descarta para que el GC la recolecte de forma natural.

3. **Mecánica de Reciclaje y Reutilización (`ObtenerOCrearTabla`)**:
   Al solicitarse una sesión para una arena (`ValueLINQStateManager.cs:67-115`), `ObtenerOCrearTabla` intenta extraer una tabla del pool con `_poolTablas.TryDequeue(out TablaSesiones<T>? reciclada)`.
   - Si la extracción es exitosa (`isReciclada == true`), se invoca `reciclada.Reiniciar(idArena, generacion)`.
   - Si el pool está vacío, se instancia una nueva `TablaSesiones<T>` (`new(idArena, generacion)`).
   - La tabla se publica en `_tablas[idArena]` con `Interlocked.CompareExchange`. Si otro hilo ganó la carrera de inicialización, la tabla reciclada se reencola de forma segura en el pool.

4. **El Método `Reiniciar(nuevoIdArena, nuevaArenaGen)`**:
   El reinicio de la tabla (`TablaSesiones.cs:404-445`) prepara la instancia para su nuevo ciclo de vida bajo sección crítica del stack (`_spinLockStack`):
   * Actualiza volátilmente `_arenaId` y `_arenaGen`.
   * Reconstituye en reversa el arreglo `_indicesLibresStack` con los índices $0$ a $4095$ y reestablece `_topStack = _capacidadMaxima`.
   * Pone a cero el marcador temporal `_vaciaDesde`.
   * Barre las particiones que ya estaban materializadas: si quedase algún búfer activo por cierres abruptos, lo devuelve a `ArrayPool<T>.Shared.Return`, sella `TamañoActual = 0`, `IsDisposed = true`, `UltimoAcceso = -1`, escribe `Token = 0L` y restablece un `SpinLock` limpio por ranura.

### Invariante de Rendimiento: 0 Bytes en Heap en Estado Estacionario

Gracias a este esquema, el coste de asignación en heap para un ciclo completo de vida de una arena explícita en estado estacionario (creación, consultas múltiples `Where`/`Select`, materialización y disposición) pasa de los ~25.7 KB originales a:

$$\mathbf{0\text{ B en el Heap de GC (medido con BenchmarkDotNet y }GC.GetAllocatedBytesForCurrentThread())}$$

El arnés de diagnóstico `ArrayPoolDiagnosticsListener` (`JCarrillo.AOT.Core.Tests/Diagnostico/ArrayPoolDiagnosticsListener.cs`) certifica físicamente que durante 50 ciclos consecutivos de reciclaje multihilo, el balance de memoria en heap es de **0 B exactos** y que el balance neto de búferes en `ArrayPool` es idéntico a cero (`BufferReturnedCount == BufferRentedCount`).

---

## 6. Estado Actual y Hoja de Ruta

### Implementado
*   **Handle `ValueLINQArena` y Semántica de Valor**: struct inmutable de 8 bytes (`Crear`/`Dispose`/`IsViva`), arena 0 ambiente y persistente, implementación estricta de `IEquatable<ValueLINQArena>`, sobrecarga de operadores `==` y `!=`, `.Equals(object?)` sin boxing en el heap, `GetHashCode()` uniforme y propiedad pública `Id` (0-4095) para telemetría y observabilidad (`ValueLINQArena.cs`).
*   **Pool de Tablas Recicladas (`TablaSesiones<T> Pooling`)**: reutilización en memoria de instancias de `TablaSesiones<T>` mediante `Reiniciar(nuevoIdArena, nuevaArenaGen)`, gestionadas a través de una cola concurrente acotada a 32 tablas por tipo en `ValueLINQStateManager<T>`, erradicando las asignaciones de heap de 25.7 KB a **0 B en estado estacionario (medido)** (`ValueLINQStateManager.cs:16-115`, `TablaSesiones.cs:404-445`).
*   **Puntos de Entrada Completos para Colecciones y Spans**: sobrecargas `ToValueQuery(arena)` y `ToValueRefQuery(arena)` para `T[]`, `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `PooledList<T>` y `PooledArray<T>`, junto con sobrecargas simétricas sin arena para `PooledArray<T>` (`ValueLINQExtensions.cs:71-330`).
*   **Blindaje de Aislamiento e Inmunidad ABA**:
    - Validación inmediata de `arena.IsViva` en todos los puntos de entrada, rechazando handles caducados y `default(ValueLINQArena)` con `ValueLinqArenaInactivaException`.
    - Token de 64 bits empaquetado `[version(28) | arena_gen(12) | arena_id(12) | slot(12)]` con versión monótona creciente (`++metadato.Version`) y validación de generación viva en `StateManager.ObtenerMetadatos` y `TablaSesiones.ObtenerMetadatos`, impidiendo colisiones entre encarnaciones del mismo identificador (`TokenHelper.cs`, `TablaSesiones.cs:207-214`).
    - Propagación incondicional del `TokenArena` a `ValueLINQStruct` y `ValueLINQRefStruct` a lo largo de todos los operadores eager (`Where`, `Select`, `Chunk`, `Concat`).
*   **Garantías de Concurrencia en el Reaper de Arenas**:
    - Guarda atómica de reentrada `_isRecolectando` con `Interlocked.CompareExchange` en `ValueLINQArenaManager.RecolectarArenas()` para prevenir tormentas de espín (*spin-storms*) ante barridos concurrentes (`ValueLINQArenaManager.cs:148`).
    - Remediación de la carrera Reaper-Sesión en `TablaSesiones.ObtenerMetadatos`: verificación de `IsArenaViva` y generación bajo el spinlock del slot antes de invocar `ArrayPool.Rent`, asegurando devolución ordenada con `PushIndice` ante carreras y erradicando búferes huérfanos (`TablaSesiones.cs:198`, `:207`).
    - Devolución segura de búferes en `LiberarMetadatos` incluso ante desactivaciones concurrentes de arena.
*   **Harness de Diagnóstico Físico (`ArrayPoolDiagnosticsListener`)**: escuchador de eventos basado en `System.Diagnostics.Tracing.EventListener` suscrito a `System.Buffers.ArrayPoolEventSource` para verificar de forma atómica y determinista que `BufferReturnedCount == BufferRentedCount` en tests E2E y de estrés.
*   **Particionamiento y Compatibilidad**: particionamiento canónico mediante `ProcesarChunks` y alias retrocompatible `ProcessChunks` marcado con `[Obsolete]` (cuya remoción definitiva ocurrirá en la versión v2.0.0).
*   **Motor Perezoso (Delay)**: entrada `ToValueDelayQuery(arena)` para `T[]`, `Span<T>` y `ReadOnlySpan<T>`; herencia de arena desde la sesión eager en `Delay()` / `ToValueDelayQuery(consulta)`; propagación por `Where`, `Select` y `Concat`; enrutado del buffer de `Chunk` a la arena; restricción de arena única y regla del receptor en `Concat`.
*   **Recolección Automática de Arenas Olvidadas**: liberación de arenas no persistentes vacías tras superar su umbral de inactividad, agregando el tiempo de vaciado por tipo y reciclando identificadores mediante cola FIFO.
*   **Regla de Mezcla de Arenas y Escotilla**: control de una sola arena explícita en el motor perezoso vía `ValueLINQArenaManager.CombinarArena` y escotilla de escape explícita `SinComprobarLimitesDeArena` en `JCarrillo.AOT.Core.ValueLINQ.Marshalling`.

> [!IMPORTANT]
> **Qué no está cubierto por pruebas**, por si condiciona tu confianza en algún camino: el reintento que hace `Alquilar` al agotarse los identificadores libres —se comprueba que sigue lanzando cuando no hay nada recuperable, pero no que recupere—, y el reciclaje real de un identificador de arena, que exigiría agotar los ~4095 disponibles en una única ejecución.
>
> El detalle de qué está verificado por mutación, qué se midió y con qué entorno está en [Verificación del sistema de arenas](Arenas.Verificacion.md).

### Propuestas de diseño (no comprometidas)
*   Enrutado a la arena de los operadores perezosos que materialicen en el futuro (`Order`, `Join`). Hoy `Chunk` es el único que reserva sesión en el motor perezoso.

> [!WARNING]
> Las propuestas de la hoja de ruta están sujetas a viabilidad y medición; no constituyen un compromiso de entrega. Solo «Implementado» describe capacidades reales.
