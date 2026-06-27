[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operadores de Materialización y Caching de Largo Ciclo de Vida

Los operadores de materialización (`ToList`, `ToArray`, `ToListRef` y `ToArrayRef`) permiten persistir el resultado de una consulta de ValueLINQ en colecciones estables fuera de la tabla de estados de `ValueLINQStateManager<T>`. Esta operación es indispensable para almacenar datos por periodos prolongados o transmitir resultados a través de fronteras asíncronas de largo ciclo de vida.

---

## 1. El Riesgo de Expiración de Sesiones

Para mantener un consumo de memoria acotado y evitar fugas de buffers, el gestor de estados `ValueLINQStateManager<T>` ejecuta una tarea de limpieza en segundo plano (`LimpiezaPeriodicTimer`) cada **1 minuto (medido)**. 

Si una sesión de consulta transitoria permanece inactiva (sin accesos de lectura o escritura) por un periodo superior al umbral configurado (por defecto, **5 minutos (medido)**):
1.  El limpiador asume que la sesión fue abandonada (por ejemplo, por la omisión del bloque `using`).
2.  La sesión es invalidada: se limpia el slot, se incrementa su versión y el buffer físico se devuelve de forma forzada a `ArrayPool<T>.Shared`.
3.  Si la aplicación intenta consumir la consulta posteriormente utilizando su token original, el StateManager detectará la discrepancia de versión e inmediatamente lanzará una excepción `ValueLinqSesionExpiradaException` o `ValueLinqTokenInvalidoException`.

Por ende, **nunca se debe almacenar una instancia de `ValueLINQStruct<T>` o `ValueLINQRefStruct<T>` en campos de clases de largo ciclo de vida o variables globales.** Para caching a largo plazo, es obligatorio materializar la consulta.

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

1.  **Colecciones Pooled Estables**: Los métodos retornan tipos como `PooledList<T>` o `PooledArray<T>`. Estas colecciones internas arriendan su almacenamiento de respaldo desde el `ArrayPool<T>.Shared`. La única reserva en el heap es el pequeño objeto wrapper de la colección (a menos que se usen variantes optimizadas en pila).
2.  **Copia en Bloque (Bulk Copy)**: En lugar de iterar elemento por elemento e insertarlos uno a uno, los materializadores realizan una transferencia de memoria contigua. Obtienen el `Span<T>` del buffer del StateManager y ejecutan una copia en bloque directo al destino:
    -   Para listas: `lista.AddRange(metadatos.Array.AsSpan(0, tamaño))`
    -   Para arrays: `metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span)`
    Esto se traduce en instrucciones de ensamblador altamente optimizadas (`rep movsd` o instrucciones vectoriales AVX/SSE según el hardware).
3.  **Liberación Atómica Inmediata**: Los métodos de materialización ejecutan la copia dentro de un bloque `try`, y en la cláusula `finally` invocan de forma determinista a `origen.Dispose()`. Esto garantiza que la sesión de consulta sea liberada y su buffer temporal regrese al pool en el instante exacto en que termina la copia, minimizando la retención de ranuras en el StateManager.

---

## 4. Ejemplo de Uso Correcto

El siguiente ejemplo muestra cómo filtrar datos y materializarlos para almacenarlos en una caché estática de la aplicación:

```csharp
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.Colecciones.Pooled;

public class CacheDeEventos
{
    // Colección estable de largo ciclo de vida que usa buffers del pool
    private static PooledList<int> _cacheIdEventosValidos;
    private static readonly object _lock = new();

    public static void InicializarCache(int[] eventosRaw)
    {
        lock (_lock)
        {
            // Liberar caché previa si existiese
            _cacheIdEventosValidos?.Dispose();

            // Construir el pipeline de consulta fluent y materializar el resultado de forma segura
            _cacheIdEventosValidos = eventosRaw
                .ToValueQuery()                       // 1. Renta un búfer del ArrayPool y crea una sesión temporal en el StateManager
                .Where(0, new FiltroEventosValidos()) // 2. Filtra elementos en pila con inlining estático del struct predicado
                .ToList();                            // 3. Copia en bloque a la lista, invoca Dispose() en cascada y libera el slot del StateManager
        }
    }

    public static ReadOnlySpan<int> ObtenerEventos()
    {
        lock (_lock)
        {
            return _cacheIdEventosValidos != null 
                ? _cacheIdEventosValidos.Span 
                : ReadOnlySpan<int>.Empty;
        }
    }
}
```

---

## 5. Materializadores Estándar (ToArrayStandard y ToListStandard)

Para simplificar la interoperabilidad con APIs convencionales de .NET que no soportan tipos pooled o que requieren una transferencia de propiedad sin exigencia de liberación manual (`Dispose()`), se añaden los materializadores estándar. Estos métodos están decorados con el atributo `[Obsolete]` para advertir al desarrollador sobre su impacto en la asignación de memoria.

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
[Obsolete("Este método genera asignaciones en el Heap al retornar un array estándar. Se permite su uso para evitar la liberación manual de recursos, pero afecta el rendimiento.", false)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static T[] ToArrayStandard<T>(this ValueLINQRefStruct<T> origen)

[Obsolete("Este método genera asignaciones en el Heap al retornar una lista estándar. Se permite su uso para evitar la liberación manual de recursos, pero afecta el rendimiento.", false)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static List<T> ToListStandard<T>(this ValueLINQRefStruct<T> origen)
```

#### Para `ValueLINQStruct<T>`
```csharp
[Obsolete("Este método genera asignaciones en el Heap al retornar un array estándar. Se permite su uso para evitar la liberación manual de recursos, pero afecta el rendimiento.", false)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static T[] ToArrayStandard<T>(this ValueLINQStruct<T> origen)

[Obsolete("Este método genera asignaciones en el Heap al retornar una lista estándar. Se permite su uso para evitar la liberación manual de recursos, pero afecta el rendimiento.", false)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static List<T> ToListStandard<T>(this ValueLINQStruct<T> origen)
```

---

## 6. Rendimiento y Mediciones Empíricas (Medido)

Las mediciones empíricas de rendimiento se registraron con BenchmarkDotNet en un procesador AMD Ryzen 9 3950X, SDK de .NET 10.0.301, en configuración Release. El harness completo está disponible en [ValueLINQBenchmarks.cs](../../../JCarrillo.AOT.Core.Benchmarks/Extensiones/ValueLINQBenchmarks.cs).

### Resultados a Escala N = 100 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 120.22 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 102.56 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 127.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 107.07 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 125.26 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 98.08 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 127.32 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 168.14 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 151.86 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 112.83 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 134.19 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 113.64 ns | 456 B | Asignación en Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArray_Pooled` | 211.68 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 163.40 ns | 424 B | Asignación en Heap (Native AOT) (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToList_Pooled` | 209.89 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToListStandard_Heap` | 176.74 ns | 456 B | Asignación en Heap (Native AOT) (medido) |

### Resultados a Escala N = 1000 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 328.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 467.07 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 353.51 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 461.80 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 203.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 291.96 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 206.01 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 299.07 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 409.09 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 513.52 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 402.30 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 310.55 ns | 4,056 B | Regresión de CPU frente a Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArray_Pooled` | 207.54 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 279.72 ns | 4,024 B | Asignación en Heap (Native AOT) (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToList_Pooled` | 230.63 ns | 0 B | Buffer reciclado en Native AOT (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToListStandard_Heap` | 284.67 ns | 4,056 B | Asignación en Heap (Native AOT) (medido) |

### Análisis de Trade-offs y Limitaciones de los Materializadores

1. **Sobrecarga de Alquiler en Pequeña Escala**:
   - En consultas cortas ($N = 100$), los materializadores estándar (`ToArrayStandard` y `ToListStandard`) resultan entre un 14.7% y un 31.5% más rápidos (medido) en tiempo de CPU que las variantes pooled. Esto se debe a la ausencia de operaciones de alquiler (`Rent`) y retorno (`Return`) de buffers contra `ArrayPool<T>.Shared`. La única excepción medida es `ToListStandard` bajo .NET 9.0 JIT, donde la versión Pooled fue un 24.3% más rápida (medido).
2. **Eficiencia en Media y Gran Escala**:
   - Para colecciones de mayor tamaño ($N = 1000$), la sobrecarga del alquiler de buffers se amortiza completamente. Las variantes pooled reducen el tiempo de CPU en un rango del 18.7% al 31.1% en la mayoría de los entornos (medido) debido a la eliminación de asignaciones redundantes de memoria heap. La única excepción a esta tendencia es `.NET 8.0 JIT`, donde `ToListStandard_Heap` (310.55 ns, medido) es un 29.5% más rápido que `ToList_Pooled` (402.30 ns, medido).
3. **Riesgo de Fugas de Recursos (Trade-off de Complejidad)**:
   - El uso de los materializadores pooled requiere que el programador gestione de manera determinista el ciclo de vida del objeto retornado (invocando `Dispose()` o mediante bloques `using`). Omitir esta llamada resulta en la fuga permanente del buffer alquilado, inhabilitando su reutilización en el pool de memoria global.
4. **Lo que estas mediciones NO evalúan**:
   - Estas pruebas se limitan a ejecuciones secuenciales en un solo hilo y no miden el coste de contención del pool de memoria bajo alta concurrencia concurrente, ni la fragmentación acumulativa del heap administrado a largo plazo. El análisis detallado de limitaciones está documentado en [Reporte de Benchmarks Consolidado](../BENCHMARK.md).

---
[Volver a Métodos y Extensiones](README.md)


