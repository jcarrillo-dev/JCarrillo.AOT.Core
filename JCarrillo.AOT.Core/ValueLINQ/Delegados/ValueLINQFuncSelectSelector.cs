using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Delegados
{
    /// <summary>
    /// Adaptador para selectores basados en delegados Func.
    /// </summary>
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly struct ValueLINQFuncSelectSelector<TOrigen, TResultado>(Func<TOrigen, TResultado> selector) : ISelectDelegado<TOrigen, TResultado>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
        where TResultado : allows ref struct
#endif
    {
        private readonly Func<TOrigen, TResultado> _selector = selector;

        /// <summary>
        /// Ejecuta la proyección del elemento especificado utilizando el delegado interno.
        /// </summary>
        /// <param name="item">El elemento de origen a transformar.</param>
        /// <returns>El resultado de la transformación.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TResultado Ejecutar(TOrigen item) => _selector(item);
    }
}
