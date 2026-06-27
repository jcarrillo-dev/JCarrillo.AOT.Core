[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Chunk y ProcessChunks

El operador `Chunk` divide una colección lógica de datos en fragmentos homogéneos de un tamaño máximo especificado. A diferencia de las implementaciones tradicionales que asignan arrays jerárquicos o sublistas en el Heap de GC, ValueLINQ realiza esta división con **cero allocations**, estructurando los fragmentos directamente como subconsultas tipo `ValueLINQStruct<T>` contenidas dentro de una consulta externa.

El procesamiento eficiente de estos fragmentos se realiza mediante el operador complementario `ProcessChunks`, que recibe un procesador estructurado para consumir cada fragmento y liberar sus recursos inmediatamente.

---

## 1. Firmas de los Operadores

Ambos operadores residen en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs).

### Sobrecargas de `Chunk`
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

### Sobrecargas de `ProcessChunks`
El operador ejecuta la lógica del procesador sobre cada chunk y asegura la liberación ordenada de toda la jerarquía de buffers temporales:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcessChunks<T, TProcessor>(
    this ValueLINQRefStruct<ValueLINQStruct<T>> listaChunks, 
    TProcessor procesarChunk)
    where TProcessor : struct, IProcesarChunkDelegado<T>
```

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ProcessChunks<T, TProcessor>(
    this ValueLINQStruct<ValueLINQStruct<T>> listaChunks, 
    TProcessor procesarChunk)
    where TProcessor : struct, IProcesarChunkDelegado<T>
```

---

## 2. Funcionamiento Interno y Asignación Cero

El operador `Chunk` funciona bajo el siguiente flujo estrictamente síncrono:

1.  **Cálculo de Ranuras**: Se calcula la cantidad de fragmentos necesarios como $\lceil N / S \rceil$ (donde $N$ es el tamaño de la colección original y $S$ es el tamaño del chunk).
2.  **Renta del Contenedor**: Se solicita a `ValueLINQStateManager<ValueLINQStruct<T>>` una nueva sesión con capacidad para albergar las estructuras `ValueLINQStruct<T>`. Esto reserva un array temporal de structs en el StateManager, sin alocar memoria en el heap.
3.  **Renta de Sub-Buffers**: Se itera sobre la colección de origen en bloques de tamaño $S$. Para cada bloque, se crea una nueva estructura `ValueLINQStruct<T>` con la capacidad exacta requerida. El StateManager asigna un slot y renta un buffer físico desde el `ArrayPool<T>`.
4.  **Copia en Bloque**: Los elementos correspondientes al fragmento se copian vectorialmente en un solo paso mediante `Span.CopyTo` directo desde el buffer de origen al sub-buffer rentado. La estructura del chunk se añade al array del contenedor.
5.  **Procesamiento y Liberación en Pipeline (`ProcessChunks`)**: `ProcessChunks` recorre el contenedor externo. Para cada fragmento, ejecuta el procesador estructurado bajo un bloque `using` (`using (var c = array[i])`). El método `Dispose` de cada chunk devuelve su buffer al `ArrayPool<T>` inmediatamente después de ser procesado.
6.  **Garantía de Limpieza en Errores**: Si ocurre una excepción durante la creación de los chunks o durante su procesamiento, el bloque `finally` de los operadores intercepta el error, recorre las sesiones creadas activas y ejecuta `Dispose` en cada una de ellas antes de liberar la sesión contenedora, eliminando cualquier riesgo de fugas de memoria o buffers huérfanos en el `ArrayPool`.

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

---

## 4. Ejemplo de Uso Correcto

Implementación de un procesador estructurado para imprimir datos en consola agrupados en bloques de 10:

```csharp
using System;
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

public struct ImpresorDeBloques : IProcesarChunkDelegado<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Ejecutar(ValueLINQStruct<int> listaChunk)
    {
        Console.WriteLine($"--- Procesando bloque de tamaño {listaChunk.IsValido} ---");
        // Iteración libre de allocations sobre el fragmento de datos
        foreach (ref int valor in listaChunk)
        {
            Console.WriteLine(valor);
        }
        // Al terminar este método, el bloque "using" interno de ProcessChunks
        // ejecutará automáticamente listaChunk.Dispose() retornando el buffer al pool.
    }
}

public static void ProcesarEnFragmentos(int[] datos)
{
    // Construir la consulta, fragmentar en bloques de 10 elementos y procesar cada bloque de forma síncrona
    datos.ToValueQuery()                          // 1. Renta un buffer del ArrayPool y crea la sesión inicial en el StateManager
         .Chunk(10)                               // 2. Divide la consulta en fragmentos en pila sin allocations en el heap
         .ProcessChunks(new ImpresorDeBloques()); // 3. Ejecuta el procesador sobre cada bloque e invoca la liberación automática en cascada
}
```

---
[Volver a Métodos y Extensiones](README.md)

