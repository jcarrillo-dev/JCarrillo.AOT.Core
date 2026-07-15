[Volver al Núcleo de ValueLINQ](README.md)

# Arenas de Memoria en ValueLINQ

Una **arena** es un ámbito de memoria explícito para las consultas de ValueLINQ. Todas las sesiones creadas dentro de una arena —incluidas las sesiones intermedias que generan los operadores— se liberan **en bloque** al disponer la arena, aunque el código cliente olvide liberar consultas individuales.

El modelo es el de las *regiones* / *arenas* clásicas (equivalente conceptual a `MemoryContext` de PostgreSQL o al ámbito *scoped* de la inyección de dependencias): se paga la reserva una vez, se usa sin ceremonia dentro del ámbito, y se recupera todo de golpe al cerrar.

> [!NOTE]
> **Estado**: funcionalidad implementada y cubierta por la batería de tests (creación, disposición en bloque, aislamiento entre arenas, propagación por los operadores eager, y las carreras de concurrencia). **No** dispone todavía de benchmarks publicados ni forma parte de un release etiquetado; las cifras de memoria de este documento son **derivadas por fórmula (estimado)**, no medidas con BenchmarkDotNet.

---

## 1. La Arena Ambiente (Arena 0)

El sistema reserva la **arena `0`** como **ambiente, interna y persistente**:

*   **Ambiente**: es la arena que se usa por defecto cuando una consulta se crea **sin** especificar arena. `datos.ToValueQuery()` (sin argumento de arena) crea su sesión en la arena 0. Todo el comportamiento previo de ValueLINQ es, en la práctica, "trabajar sobre la arena 0".
*   **Persistente**: la arena 0 se pre-alquila durante la inicialización del gestor de arenas y está marcada como persistente. **No puede liberarse**: cualquier intento de disponerla es un no-op seguro, y jamás es candidata a la recolección automática por inactividad. Vive durante todo el proceso.
*   **Interna**: el usuario no la crea ni la gestiona. No existe un handle público hacia la arena 0; simplemente es el destino implícito de las consultas sin arena.

En consecuencia, **usar ValueLINQ sin arenas explícitas sigue funcionando exactamente igual que antes**: se opera sobre la arena 0, que nunca desaparece. Las arenas explícitas son una capacidad *opcional* que se añade encima, no un requisito.

---

## 2. Uso de Arenas Explícitas

El handle público es `ValueLINQArena`, un `readonly struct` de 8 bytes que solo transporta un token de arena (identificador + generación); el almacenamiento real vive en las tablas de sesión por tipo del `ValueLINQStateManager<T>`.

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
| `static ValueLINQArena Crear(bool persistente = false)` | Alquila una arena nueva. El parámetro `persistente` está **reservado** para la futura recolección automática por inactividad (no implementada); en la versión actual no altera el comportamiento. |
| `void Dispose()` | Libera la arena y **todas** las sesiones de ValueLINQ creadas dentro de ella, en cualquier tipo `T`. |
| `bool IsViva` | Indica si la arena sigue activa (no liberada). |

> [!IMPORTANT]
> **La disposición de arenas explícitas es obligatoria en la versión actual.** No existe todavía recolección automática de arenas olvidadas: una arena no dispuesta retiene su identificador (y el cascarón de sus tablas de sesión) hasta el fin del proceso. Los *buffers* de las sesiones internas sí se siguen reciclando por el limpiador de fondo, pero agotar los ~4095 identificadores de arena disponibles hace fallar la creación de nuevas arenas de forma permanente. Usa `using` o `Dispose` explícito; considera la arena ambiente (arena 0, sin argumento) si no necesitas un ámbito propio.

### Puntos de entrada con arena

Se ofrecen sobrecargas de los inicializadores que aceptan la arena de destino:

```csharp
public static ValueLINQStruct<T>    ToValueQuery<T>(this T[] origen, ValueLINQArena arena);
public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this T[] origen, ValueLINQArena arena);
```

### Propagación por los operadores

Los operadores eager (`Where`, `Select`, `Chunk`, `Concat`) crean su sesión de destino **en la misma arena que su origen**, deduciéndola del token de la consulta de entrada. Esto garantiza que una cadena iniciada en una arena mantiene *todas* sus sesiones intermedias dentro de esa arena, de modo que la disposición en bloque las alcanza todas.

El aislamiento cruzado por tipo funciona porque la disposición de una arena se difunde a **todos** los tipos `T` que hayan registrado tablas en ella. Un `Select<int, …, string>` crea su destino en la tabla de `string` de esa arena, y disponer la arena también la libera.

> [!NOTE]
> **`Concat` toma la arena de su primer operando y prohíbe mezclar arenas.** El destino se crea en la arena del primer operando; si cualquier otro operando pertenece a una arena distinta, se lanza `ValueLinqArenaCruzadaException` (acceso cruzado entre arenas). Una consulta *default* (token cero) no impone arena y se omite en la comprobación. Así, combinar sesiones de arenas diferentes es un error explícito en vez de una violación silenciosa de aislamiento.

---

## 3. Estructura del Token y Aislamiento entre Encarnaciones

El token de sesión de 64 bits reparte sus bits así:

```
[ version (28) | arena_gen (12) | arena_id (12) | slot (12) ]
```

*   **`arena_id`** (12 bits): a qué arena pertenece la sesión (hasta 4096 arenas simultáneas, incluida la 0).
*   **`arena_gen`** (12 bits): la generación de la arena. Como los identificadores de arena se reciclan, la generación desambigua **encarnaciones distintas del mismo id**: una sesión de una arena ya liberada no puede confundirse con una sesión de una arena nueva que reutilice ese id, aunque coincidan slot y versión.
*   **`slot`** (12 bits) y **`version`** (28 bits): identidad y protección ABA de la sesión dentro de su tabla, como en el diseño base.

El reciclado de identificadores de arena es **FIFO**: un id liberado no se reutiliza hasta haber ciclado por los demás, lo que refuerza el margen de la generación de arena.

---

## 4. Contrato de Vida Útil

> [!IMPORTANT]
> **Una sesión solo es válida mientras su arena esté viva.** Usar una consulta (o un token de sesión) después de disponer su arena es *comportamiento indefinido*, análogo a un *use-after-free* de un ámbito. Es el mismo contrato que el de cualquier sistema de arenas/regiones (en Rust lo impone el compilador; aquí se documenta).

El sistema **falla de forma segura** ante esta clase de uso: gracias a la validación por token y a la generación de arena, una sesión de una arena dispuesta se detecta como inválida (los caminos de solo lectura devuelven `false` / no-op; los caminos de trabajo lanzan `ValueLinqArenaInactivaException` o `ValueLinqSesionExpiradaException`). No hay corrupción silenciosa de datos.

---

## 5. Coste de Memoria (estimado, derivado por fórmula)

*   **Entrada por `(tipo, arena)`**: la primera sesión de un tipo `T` en una arena materializa su primera partición de la tabla de sesiones. Coste de entrada ≈ el stack de índices + una hoja de partición; del orden de decenas de KB por `(T, arena)` **(estimado)**, no los cientos de KB del diseño global anterior.
*   **Índice de tablas por tipo**: cada `ValueLINQStateManager<T>` mantiene un array de punteros a tabla por arena (`Arenas` entradas), inicializado a nulos.

Estas cifras se derivan de la geometría configurada en `ValueLINQConfig` y **no** están medidas con BenchmarkDotNet; se etiquetan como estimadas conforme al estándar de ingeniería honesta del proyecto.

---

## 6. Estado Actual y Hoja de Ruta

### Implementado y verificado por tests
*   Handle `ValueLINQArena` (`Crear`/`Dispose`/`IsViva`), arena 0 ambiente y persistente.
*   Entrada `ToValueQuery(arena)` / `ToValueRefQuery(arena)` para arrays.
*   Propagación de arena en los operadores eager (`Where`, `Select`, `Chunk`, `Concat`), incluido el fan-out cruzado por tipo.
*   Aislamiento entre arenas, disposición en bloque, y cierre de las carreras de concurrencia (creación-vs-liberación de tabla con revalidación por generación; reciclado FIFO de ids; ABA entre encarnaciones vía `arena_gen`).

### Propuestas de diseño (no comprometidas)
*   **Recolección automática de arenas olvidadas (reaper en cascada)**: un recolector de fondo que libere arenas no persistentes, vacías e inactivas. Pendiente de decisión de diseño (umbrales, refresco de inactividad).
*   **Pool de tablas recicladas** para que crear/destruir arenas no asigne en estado estacionario.
*   Sobrecargas de entrada con arena para `Span`, `Memory` y `PooledList`.

> [!WARNING]
> Las propuestas de la hoja de ruta están sujetas a viabilidad y medición; no constituyen un compromiso de entrega. Solo "Implementado y verificado" describe capacidades reales.
