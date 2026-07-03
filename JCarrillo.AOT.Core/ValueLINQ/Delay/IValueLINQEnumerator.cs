#if NET9_0_OR_GREATER

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Interfaz base para los enumeradores de la canalización de evaluación perezosa (lazy).
    /// </summary>
    public interface IValueLINQEnumerator<T> : IDisposable
        where T : allows ref struct
    {
        /// <summary>
        /// Desplaza el enumerador al siguiente elemento del flujo de datos evaluado de forma perezosa.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final del flujo.</returns>
        bool MoveNext();

        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia de solo lectura al elemento actual de tipo <typeparamref name="T"/>.</value>
        ref readonly T Current { get; }
    }
}
#endif
