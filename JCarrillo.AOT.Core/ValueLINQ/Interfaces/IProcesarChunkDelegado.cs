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
}
