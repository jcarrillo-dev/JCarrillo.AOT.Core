namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    /// <summary>
    /// Define un delegado estructural para procesar fragmentos (chunks) de elementos de forma eficiente y sin asignaciones en el montón.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos del fragmento.</typeparam>
    public interface IProcesarChunkDelegado<T>
    {
        /// <summary>
        /// Ejecuta el procesamiento sobre el fragmento especificado.
        /// </summary>
        /// <param name="listaChunk">El fragmento a procesar.</param>
        void Ejecutar(ValueLINQStruct<T> listaChunk);
    }

    /// <summary>
    /// Define un delegado estructural de alto rendimiento para procesar fragmentos (chunks) expuestos como intervalos de solo lectura sin provocar asignaciones en el heap.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos contenidos en el fragmento.</typeparam>
    public interface IProcesarChunkRefDelegado<T>
    {
        /// <summary>
        /// Ejecuta el procesamiento lógico correspondiente sobre el fragmento de memoria contigua especificado.
        /// </summary>
        /// <param name="listaChunk">El fragmento expuesto como <see cref="ReadOnlySpan{T}"/> a procesar.</param>
        void Ejecutar(ReadOnlySpan<T> listaChunk);
    }
}
