namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    /// <summary>
    /// Define un delegado estructural para evaluar si un elemento de tipo <typeparamref name="TOrigen"/> cumple una condición en relación con un valor de tipo <typeparamref name="TDato"/>.
    /// </summary>
    /// <typeparam name="TOrigen">El tipo del elemento de origen.</typeparam>
    /// <typeparam name="TDato">El tipo del dato de comparación.</typeparam>
    public interface IWhereDelegado<TOrigen, TDato>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
        where TDato : allows ref struct
#endif
    {
        /// <summary>
        /// Evalúa la condición sobre el elemento especificado.
        /// </summary>
        /// <param name="objetoLista">El elemento a evaluar.</param>
        /// <param name="otro">El valor de comparación.</param>
        /// <returns><see langword="true"/> si el elemento cumple la condición; de lo contrario, <see langword="false"/>.</returns>
        bool Ejecutar(TOrigen objetoLista, TDato otro);
    }
}
