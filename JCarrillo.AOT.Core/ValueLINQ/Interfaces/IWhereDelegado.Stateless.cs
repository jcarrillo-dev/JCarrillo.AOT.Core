namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    /// <summary>
    /// Define un delegado estructural para evaluar si un elemento de tipo <typeparamref name="TOrigen"/> cumple una condición sin requerir estado.
    /// </summary>
    /// <typeparam name="TOrigen">El tipo del elemento de origen.</typeparam>
    public interface IWhereDelegado<TOrigen>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
#endif
    {
        /// <summary>
        /// Evalúa la condición sobre el elemento especificado.
        /// </summary>
        /// <param name="objetoLista">El elemento a evaluar.</param>
        /// <returns><see langword="true"/> si el elemento cumple la condición; de lo contrario, <see langword="false"/>.</returns>
        bool Ejecutar(TOrigen objetoLista);
    }
}
