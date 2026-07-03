namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    /// <summary>
    /// Define un delegado estructural para proyectar un elemento de tipo <typeparamref name="TOrigen"/> a otro de tipo <typeparamref name="TResultado"/> de forma eficiente.
    /// </summary>
    /// <typeparam name="TOrigen">El tipo del elemento de origen.</typeparam>
    /// <typeparam name="TResultado">El tipo del elemento de resultado.</typeparam>
    public interface ISelectDelegado<TOrigen, TResultado>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
        where TResultado : allows ref struct
#endif
    {
        /// <summary>
        /// Proyecta el elemento especificado.
        /// </summary>
        /// <param name="objetoLista">El elemento a proyectar.</param>
        /// <returns>El elemento proyectado.</returns>
        TResultado Ejecutar(TOrigen objetoLista);
    }
}
