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
[Volver a Métodos y Extensiones](README.md)


