[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Chunk y ProcesarChunks

El operador `Chunk` divide una colección lógica de datos en fragmentos homogéneos de un tamaño máximo especificado. A diferencia de las implementaciones tradicionales que asignan arrays jerárquicos o sublistas en el Heap de GC, ValueLINQ realiza esta división con **cero allocations**, estructurando los fragmentos directamente como subconsultas tipo `ValueLINQStruct<T>` contenidas dentro de una consulta externa. Esta descripción corresponde al motor Eager; el motor Lazy/Diferido (Delay) produce los fragmentos como `ReadOnlySpan<T>` sin materializar subconsultas.

El procesamiento eficiente de estos fragmentos se realiza mediante el operador complementario `ProcesarChunks`, que recibe un procesador estructurado para consumir cada fragmento y liberar sus recursos inmediatamente.

---

## 1. Firmas de los Operadores

Las variantes del motor Eager residen en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs); las variantes del motor Delay residen en [ValueLINQDelayExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayExtensions.cs) y [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs).

### 1.1 Motor Eager (Alquiler de Búferes)

#### Sobrecargas de `Chunk`
El operador divide una sesión activa y retorna un contenedor externo de chunks:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQRefStruct<ValueLINQStruct<T>> Chunk<T>(
    this ValueLINQRefStruct<T> origen, 
    int tamaño)
```

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQRefStruct<ValueLINQStruct<T>> Chunk<T>(
    this ValueLINQStruct<T> origen, 
    int tamaño)
```

#### Sobrecargas de `ProcesarChunks`
El operador ejecuta la lógica del procesador sobre cada chunk y asegura la liberación ordenada de toda la jerarquía de buffers temporales:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcesarChunks<T, TProcessor>(
    this ValueLINQRefStruct<ValueLINQStruct<T>> listaChunks, 
    TProcessor procesarChunk)
    where TProcessor : struct, IProcesarChunkDelegado<T>
```

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcesarChunks<T, TProcessor>(
    this ValueLINQStruct<ValueLINQStruct<T>> listaChunks, 
    TProcessor procesarChunk)
    where TProcessor : struct, IProcesarChunkDelegado<T>
```

### 1.2 Motor Lazy/Diferido (Delay)

#### Operador `Chunk` Perezoso
La variante perezosa agrupa el flujo de datos en fragmentos expuestos como `ReadOnlySpan<T>` mediante el enumerador `ref struct` [ValueLINQChunkDelay.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQChunkDelay.cs), sin materializar subconsultas `ValueLINQStruct<T>`:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQDelayStruct<ReadOnlySpan<T>, ValueLINQChunkDelay<T, TEnumerator>> Chunk<T, TEnumerator>(
    this ValueLINQDelayStruct<T, TEnumerator> pipeline, 
    int chunkSize)
    where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
```

#### Operadores Terminales `ProcesarChunk` y `ProcesarChunkRef`
Consumen la canalización de fragmentos de forma síncrona. Ambos exigen la restricción `IProcesarChunkRefDelegado<T>` sobre el procesador. `ProcesarChunk` recibe un procesador struct mutable pasado por referencia:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcesarChunk<T, TEnumerator, TProcesador>(
    this ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline, 
    ref TProcesador procesar)
    where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
    where TProcesador : struct, IProcesarChunkRefDelegado<T>
```

`ProcesarChunkRef` admite procesadores `ref struct` pasados por valor:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcesarChunkRef<T, TEnumerator, TProcesador>(
    this ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline, 
    TProcesador procesar)
    where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
    where TProcesador : IProcesarChunkRefDelegado<T>, allows ref struct
```

#### Sobrecarga Ergonómica (Delegado)
Permite consumir los fragmentos con una lambda o delegado tradicional basado en `ProcesarChunkDelegado<T>`. Reside en [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs) y, al igual que las sobrecargas ergonómicas de `Where` y `Select`, está marcada como `[Obsolete]` con el diagnóstico [JCA0001](../../Diagnostico/JCA/JCA0001.md), porque la lambda o el delegado pueden generar asignaciones en el heap:

```csharp
public delegate void ProcesarChunkDelegado<T>(scoped ref ReadOnlySpan<T> chunk);

[Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcesarChunk<T, TEnumerator>(
    this ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline,
    ProcesarChunkDelegado<T> procesar)
    where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
```

Para rutas calientes, usar la variante estructurada `ProcesarChunkRef` con un procesador que implemente `IProcesarChunkRefDelegado<T>`, o declarar la lambda como `static` para evitar la clase de clausura.

Toda la variante Delay está compilada bajo `#if NET9_0_OR_GREATER`: el motor Delay requiere soporte del runtime para la restricción genérica `allows ref struct` de C# 13.

---

## 2. Funcionamiento Interno y Asignación Cero

El operador `Chunk` funciona bajo el siguiente flujo estrictamente síncrono:

1.  **Cálculo de Ranuras**: Se calcula la cantidad de fragmentos necesarios como $\lceil N / S \rceil$ (donde $N$ es el tamaño de la colección original y $S$ es el tamaño del chunk).
2.  **Renta del Contenedor**: Se solicita a `ValueLINQStateManager<ValueLINQStruct<T>>` una nueva sesión con capacidad para albergar las estructuras `ValueLINQStruct<T>`, **en la misma arena que la consulta de origen**: el operador extrae el identificador de arena del token de origen mediante `TokenHelper.ObtenerArenaId(origenToken)` y crea el contenedor con `new ValueLINQRefStruct<ValueLINQStruct<T>>(arenaId, cantidadChunks)`. Esto reserva un array temporal de structs en el StateManager, sin alocar memoria en el heap.
3.  **Renta de Sub-Buffers**: Se itera sobre la colección de origen en bloques de tamaño $S$. Para cada bloque, se crea una nueva estructura `ValueLINQStruct<T>` con la capacidad exacta requerida, igualmente en la arena del origen mediante `ValueLINQStruct<T> chunk = new(TokenHelper.ObtenerArenaId(origenToken), chunkSize)`. El StateManager asigna el slot en la `TablaSesiones` correspondiente a esa arena y renta un buffer físico desde el `ArrayPool<T>`.
4.  **Copia en Bloque**: Los elementos correspondientes al fragmento se copian vectorialmente en un solo paso mediante `Span.CopyTo` directo desde el buffer de origen al sub-buffer rentado. La estructura del chunk se añade al array del contenedor.
5.  **Procesamiento y Liberación en Pipeline (`ProcesarChunks`)**: `ProcesarChunks` recorre el contenedor externo. Para cada fragmento, ejecuta el procesador estructurado bajo un bloque `using` (`using (var c = array[i])`). El método `Dispose` de cada chunk devuelve su buffer al `ArrayPool<T>` inmediatamente después de ser procesado.
6.  **Garantía de Limpieza en Errores**: Si ocurre una excepción durante la creación de los chunks o durante su procesamiento, el bloque `finally` de los operadores intercepta el error, recorre las sesiones creadas activas y ejecuta `Dispose` en cada una de ellas antes de liberar la sesión contenedora, eliminando cualquier riesgo de fugas de memoria o buffers huérfanos en el `ArrayPool`.

> **Nota — Propagación de arena**: el ciclo de vida de la sesión contenedora y de las sesiones de cada chunk queda gobernado por la arena de la consulta de origen; si dicha arena ya no está activa al crear u operar sobre las sesiones, el StateManager lanza `ValueLinqArenaInactivaException` ([ValueLINQStateManager.cs](../../../JCarrillo.AOT.Core/ValueLINQ/ValueLINQStateManager.cs)). Un token de origen `0` (consulta vacía/default) no impone arena y las sesiones se crean en la arena por defecto (arena 0). Consulta [Arenas.md](../Core/Arenas.md) para el detalle del sistema de arenas.

---

## 3. Abstracción del Procesador

Para consumir los fragmentos, se debe definir un struct que implemente la interfaz [IProcesarChunkDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IProcesarChunkDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IProcesarChunkDelegado<T>
    {
        void Ejecutar(ValueLINQStruct<T> listaChunk);
    }
}
```

En el mismo archivo se declara la interfaz homóloga para el motor Delay:

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IProcesarChunkRefDelegado<T>
    {
        void Ejecutar(ReadOnlySpan<T> listaChunk);
    }
}
```

La variante Eager consume los fragmentos como `ValueLINQStruct<T>` (`IProcesarChunkDelegado<T>`), mientras que la variante Delay los consume como `ReadOnlySpan<T>` (`IProcesarChunkRefDelegado<T>`).

---

## 4. Ejemplo de Uso Correcto

Implementación de un procesador estructurado para imprimir datos en consola agrupados en bloques de 10:

```csharp
using System;
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

public struct ImpresorDeBloques : IProcesarChunkDelegado<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Ejecutar(ValueLINQStruct<int> listaChunk)
    {
        Console.WriteLine($"--- Procesando bloque (válido: {listaChunk.IsValido}) ---");
        // Iteración libre de allocations sobre el fragmento de datos
        int conteo = 0;
        foreach (ref int valor in listaChunk)
        {
            Console.WriteLine(valor);
            conteo++;
        }
        Console.WriteLine($"--- Bloque procesado: {conteo} elementos ---");
        // Al terminar este método, el bloque "using" interno de ProcesarChunks
        // ejecutará automáticamente listaChunk.Dispose() retornando el buffer al pool.
    }
}

public static void ProcesarEnFragmentos(int[] datos)
{
    // Construir la consulta, fragmentar en bloques de 10 elementos y procesar cada bloque de forma síncrona
    datos.ToValueQuery()                          // 1. Renta un buffer del ArrayPool y crea la sesión inicial en el StateManager
         .Chunk(10)                               // 2. Divide la consulta en fragmentos en pila sin allocations en el heap
         .ProcesarChunks(new ImpresorDeBloques()); // 3. Ejecuta el procesador sobre cada bloque e invoca la liberación automática en cascada
}
```

---
[Volver a Métodos y Extensiones](README.md)

