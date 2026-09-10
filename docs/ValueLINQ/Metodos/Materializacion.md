[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operadores de Materialización y Caching de Largo Ciclo de Vida

Los operadores de materialización (`ToList`, `ToArray`, `ToListRef` y `ToArrayRef`) permiten persistir el resultado de una consulta de ValueLINQ en colecciones estables fuera de la tabla de estados de `ValueLINQStateManager<T>`. Esta operación es indispensable para almacenar datos por periodos prolongados o transmitir resultados a través de fronteras asíncronas de largo ciclo de vida.

---

## 1. El Riesgo de Expiración de Sesiones

Para mantener un consumo de memoria acotado y evitar fugas de buffers, la limpieza en segundo plano la ejecuta el servicio compartido `ValueLINQGC` mediante un `PeriodicTimer` que dispara cada **10 segundos** (`ValueLINQConfig.IntervaloGC`). Cada `ValueLINQStateManager<T>` registra en ese servicio su rutina `LimpiarExpirados`, que recorre las tablas de sesiones de todas las arenas y libera las sesiones cuya inactividad supera el umbral configurado en `ValueLINQStateManager<T>.TiempoLimpieza`.

Si una sesión de consulta transitoria permanece inactiva (sin accesos de lectura o escritura) por un periodo superior al umbral configurado (por defecto, **5 minutos**, definido en `ValueLINQConfig.TiempoLimpiezaPorDefecto`; al configurar `TiempoLimpieza`, el valor mínimo admitido es de **1 minuto**, `ValueLINQConfig.TiempoLimpiezaMinimo`, que es un límite de configuración y no la cadencia del timer):
1.  El limpiador asume que la sesión fue abandonada (por ejemplo, por la omisión del bloque `using`).
2.  La sesión es invalidada: se limpia el slot (se anulan sus metadatos y su token almacenado se pone a 0) y el buffer físico se devuelve de forma forzada a `ArrayPool<T>.Shared`. La versión del slot no se incrementa en ese momento, sino cuando el slot se reutiliza para una nueva sesión, con protección para no generar nunca un token 0.
3.  Si la aplicación intenta consumir la consulta posteriormente utilizando su token original, el StateManager detectará la discrepancia con el token almacenado en el slot y lanzará `ValueLinqSesionExpiradaException`; la excepción `ValueLinqTokenInvalidoException` queda reservada a tokens 0 (consultas default no inicializadas).

Por ende, **nunca se debe almacenar una instancia de `ValueLINQStruct<T>` o `ValueLINQRefStruct<T>` en campos de clases de largo ciclo de vida o variables globales.** Para caching a largo plazo, es obligatorio materializar la consulta.

### Cierre de Arenas: el Segundo Camino de Invalidación

La expiración por inactividad no es el único riesgo de ciclo de vida. Las consultas creadas con los overloads `ToValueQuery(arena)` o `ToValueRefQuery(arena)` quedan adscritas a una `ValueLINQArena`: al invocar `Dispose()` sobre la arena, todas sus sesiones se liberan en bloque de forma inmediata, con independencia del umbral de inactividad de 5 minutos. Cualquier intento posterior de consumir o materializar una consulta adscrita a una arena ya liberada lanza `ValueLinqArenaInactivaException`.

Por ello, los datos que deban sobrevivir al cierre de la arena deben materializarse con `ToList`/`ToArray` (o sus variantes `ToListRef`/`ToArrayRef`, o los materializadores estándar) **antes** de liberar la arena: las colecciones resultantes arriendan sus buffers directamente de `ArrayPool<T>.Shared` y no dependen de la tabla de sesiones, por lo que permanecen válidas tras el `Dispose()` de la arena.

Esa misma propiedad es la que permite **trasladar datos de una arena a otra**, ya que una consulta no puede abarcar dos arenas explícitas: se materializa fuera de ambas y se vuelve a entrar en consulta sobre la arena destino. El procedimiento, con ejemplo y con el motivo de la restricción, está en [Arenas de Memoria § Pasar datos de una arena a otra](../Core/Arenas.md#pasar-datos-de-una-arena-a-otra).

---

## 2. Firmas de los Operadores

Los operadores están disponibles en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs) para ambas variantes de consulta:

### Para `ValueLINQRefStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledList<T> ToList<T>(this ValueLINQRefStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledListRef<T> ToListRef<T>(this ValueLINQRefStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledArray<T> ToArray<T>(this ValueLINQRefStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledArrayRef<T> ToArrayRef<T>(this ValueLINQRefStruct<T> origen)
```

### Para `ValueLINQStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledList<T> ToList<T>(this ValueLINQStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledListRef<T> ToListRef<T>(this ValueLINQStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledArray<T> ToArray<T>(this ValueLINQStruct<T> origen)

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static PooledArrayRef<T> ToArrayRef<T>(this ValueLINQStruct<T> origen)
```

---

## 3. Transferencia Vectorial con Cero Allocations Adicionales

La materialización en ValueLINQ no compromete el objetivo de cero allocations en el heap gracias a su integración con las colecciones de alto rendimiento del proyecto:

1.  **Colecciones Pooled Estables**: Los métodos retornan tipos como `PooledList<T>` o `PooledArray<T>`. Estas colecciones internas arriendan su almacenamiento de respaldo desde el `ArrayPool<T>.Shared`. Dado que `PooledList<T>` y `PooledArray<T>` son structs (tipos por valor), la materialización no reserva ningún objeto wrapper en el heap: la colección retornada vive en la pila del llamador y su almacenamiento de respaldo se alquila de `ArrayPool<T>.Shared`, que recicla buffers en lugar de asignar memoria nueva (coherente con los 0 B medidos en los benchmarks de la sección 6). Las variantes `ToListRef` y `ToArrayRef` retornan `ref struct` (`PooledListRef<T>` y `PooledArrayRef<T>`), cuyo confinamiento a la pila lo garantiza además el propio compilador.
2.  **Copia en Bloque (Bulk Copy)**: En lugar de iterar elemento por elemento e insertarlos uno a uno, los materializadores realizan una transferencia de memoria contigua. Obtienen el `Span<T>` del buffer del StateManager y ejecutan una copia en bloque directo al destino:
    -   Para listas: `lista.AddRange(metadatos.Array.AsSpan(0, tamaño))`
    -   Para arrays: `metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span)`
    Esto se traduce en instrucciones de ensamblador altamente optimizadas (`rep movsd` o instrucciones vectoriales AVX/SSE según el hardware).
3.  **Liberación Atómica Inmediata**: Los métodos de materialización ejecutan la copia dentro de un bloque `try`, y en la cláusula `finally` invocan de forma determinista a `origen.Dispose()`. Esto garantiza que la sesión de consulta sea liberada y su buffer temporal regrese al pool en el instante exacto en que termina la copia, minimizando la retención de ranuras en el StateManager.

---

## 4. Ejemplo de Uso Correcto

El siguiente ejemplo muestra cómo filtrar datos y materializarlos para almacenarlos en una caché estática de la aplicación:

```csharp
using System;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;

public class CacheDeEventos
{
    // Colección estable de largo ciclo de vida que usa buffers del pool.
    // PooledList<T> es un record struct (tipo por valor): no admite null
    // ni el operador '?.'; la existencia de la caché se controla con un flag.
    private static PooledList<int> _cacheIdEventosValidos;
    private static bool _hayCache;
    private static readonly object _lock = new();

    public static void InicializarCache(int[] eventosRaw)
    {
        lock (_lock)
        {
            // Liberar la caché previa si existiese. Se copia la estructura a una
            // variable local en pila antes de invocar Dispose(), porque la
            // validación anti-boxing de Dispose() rechaza instancias residentes
            // en campos estáticos (fuera de la pila del hilo). Ambas copias
            // comparten el mismo búfer alquilado, que regresa al pool.
            if (_hayCache)
            {
                PooledList<int> cachePrevia = _cacheIdEventosValidos;
                cachePrevia.Dispose();
            }

            // Construir el pipeline de consulta fluent y materializar el resultado de forma segura
            _cacheIdEventosValidos = eventosRaw
                .ToValueQuery()                       // 1. Renta un búfer del ArrayPool y crea una sesión temporal en el StateManager
                .Where(0, new FiltroEventosValidos()) // 2. Filtra elementos en pila con inlining estático del struct predicado
                .ToList();                            // 3. Copia en bloque a la lista, invoca Dispose() en cascada y libera el slot del StateManager
            _hayCache = true;
        }
    }

    public static ReadOnlySpan<int> ObtenerEventos()
    {
        lock (_lock)
        {
            return _hayCache
                ? _cacheIdEventosValidos.Span
                : ReadOnlySpan<int>.Empty;
        }
    }
}
```

---

## 5. Materializadores Estándar (ToArrayStandard y ToListStandard)

Para simplificar la interoperabilidad con APIs convencionales de .NET que no soportan tipos pooled o que requieren una transferencia de propiedad sin exigencia de liberación manual (`Dispose()`), se añaden los materializadores estándar. Estos métodos están decorados con el atributo `[Obsolete]` para advertir al desarrollador sobre su impacto en la asignación de memoria. La advertencia no usa una cadena literal por método, sino el diagnóstico centralizado JCA0002, definido en [JCADiagnostico.JCA0002.cs](../../../JCarrillo.AOT.Core/Diagnostico/JCADiagnostico.JCA0002.cs): el compilador la emite con el identificador `JCA0002`, el mensaje "Este método realiza la materialización a una colección estándar e induce asignaciones en el Heap (Allocations). Considere el uso de materializadores pooled (ToList, ToArray) para preservar el perfil zero-allocation." y un enlace de ayuda (`UrlFormat`) a la [página de la wiki del diagnóstico](https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/blob/main/docs/Diagnostico/JCA/JCA0002.md). Esto permite suprimir o auditar la advertencia por su identificador en lugar de por texto.

### Propósito y Trade-offs
- **Evitar la Liberación Manual**: Al retornar arreglos nativos (`T[]`) o listas estándar (`List<T>`), el ciclo de vida del buffer queda bajo el control del recolector de basura (GC). Esto elude la necesidad de invocar `Dispose()`.
- **Asignación en el Heap (Heap Allocations)**: A diferencia de las colecciones pooled que registran alocaciones nulas (0 B), estos métodos generan asignaciones en memoria heap proporcionales al tamaño de la colección, lo que eleva el trabajo y las pausas del GC.
- **Optimización de Copia Directa**: En `ToListStandard`, se utiliza la API `CollectionsMarshal` para precalibrar el tamaño interno de la lista administrada:
  1. Se instancia la lista con la capacidad final exacta.
  2. Se modifica su longitud interna mediante `CollectionsMarshal.SetCount(lista, tamaño)`.
  3. Se copia el Span del gestor de estados directamente sobre el Span subyacente de la lista usando `CollectionsMarshal.AsSpan(lista)`.
  Esto elude la asignación de arrays temporales por redimensionado incremental y reduce el consumo de CPU.

### Firmas de los Operadores

#### Para `ValueLINQRefStruct<T>`
```csharp
[Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static T[] ToArrayStandard<T>(this ValueLINQRefStruct<T> origen)

[Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static List<T> ToListStandard<T>(this ValueLINQRefStruct<T> origen)
```

#### Para `ValueLINQStruct<T>`
```csharp
[Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static T[] ToArrayStandard<T>(this ValueLINQStruct<T> origen)

[Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static List<T> ToListStandard<T>(this ValueLINQStruct<T> origen)
```

---

## 6. Rendimiento y Mediciones Empíricas (Medido)

Las mediciones empíricas de rendimiento se registraron con BenchmarkDotNet en un procesador AMD Ryzen 9 3950X, SDK de .NET 10.0.301, en configuración Release. El harness completo está disponible en [ValueLINQBenchmarks.cs](../../../JCarrillo.AOT.Core.Benchmarks/Extensiones/ValueLINQBenchmarks.cs).

### Resultados a Escala N = 100 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayPooled` | 120.22 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 102.56 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListPooled` | 127.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListStandardHeap` | 107.07 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayPooled` | 125.26 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 98.08 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListPooled` | 127.32 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListStandardHeap` | 168.14 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayPooled` | 151.86 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 112.83 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListPooled` | 134.19 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListStandardHeap` | 113.64 ns | 456 B | Asignación en Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayPooled` | 211.68 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayStandardHeap` | 163.40 ns | 424 B | Asignación en Heap (Native AOT) (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListPooled` | 209.89 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListStandardHeap` | 176.74 ns | 456 B | Asignación en Heap (Native AOT) (medido) |

### Resultados a Escala N = 1000 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayPooled` | 328.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 467.07 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListPooled` | 353.51 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListStandardHeap` | 461.80 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayPooled` | 203.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 291.96 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListPooled` | 206.01 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListStandardHeap` | 299.07 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayPooled` | 409.09 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 513.52 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListPooled` | 402.30 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListStandardHeap` | 310.55 ns | 4,056 B | Regresión de CPU frente a Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayPooled` | 207.54 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayStandardHeap` | 279.72 ns | 4,024 B | Asignación en Heap (Native AOT) (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListPooled` | 230.63 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListStandardHeap` | 284.67 ns | 4,056 B | Asignación en Heap (Native AOT) (medido) |

### Análisis de Trade-offs y Limitaciones de los Materializadores

1. **Sobrecarga de Alquiler en Pequeña Escala**:
   - En consultas cortas ($N = 100$), los materializadores estándar (`ToArrayStandard` y `ToListStandard`) resultan entre un 14.7% y un 31.5% más rápidos (medido) en tiempo de CPU que las variantes pooled. Esto se debe a la ausencia de operaciones de alquiler (`Rent`) y retorno (`Return`) de buffers contra `ArrayPool<T>.Shared`. La única excepción medida es `ToListStandard` bajo .NET 9.0 JIT, donde la versión Pooled fue un 24.3% más rápida (medido).
2. **Eficiencia en Media y Gran Escala**:
   - Para colecciones de mayor tamaño ($N = 1000$), la sobrecarga del alquiler de buffers se amortiza completamente. Las variantes pooled reducen el tiempo de CPU en un rango del 18.7% al 31.1% en la mayoría de los entornos (medido) debido a la eliminación de asignaciones redundantes de memoria heap. La única excepción a esta tendencia es `.NET 8.0 JIT`, donde `ValueLINQStructToListStandardHeap` (310.55 ns, medido) es un 29.5% más rápido que `ValueLINQStructToListPooled` (402.30 ns, medido).
3. **Riesgo de Fugas de Recursos (Trade-off de Complejidad)**:
   - El uso de los materializadores pooled requiere que el programador gestione de manera determinista el ciclo de vida del objeto retornado (invocando `Dispose()` o mediante bloques `using`). Omitir esta llamada resulta en la fuga permanente del buffer alquilado, inhabilitando su reutilización en el pool de memoria global.
4. **Lo que estas mediciones NO evalúan**:
   - Estas pruebas se limitan a ejecuciones secuenciales en un solo hilo y no miden el coste de contención del pool de memoria bajo alta concurrencia concurrente, ni la fragmentación acumulativa del heap administrado a largo plazo. El análisis detallado de limitaciones está documentado en [Reporte de Benchmarks Consolidado](../BENCHMARK.md).

---
[Volver a Métodos y Extensiones](README.md)


