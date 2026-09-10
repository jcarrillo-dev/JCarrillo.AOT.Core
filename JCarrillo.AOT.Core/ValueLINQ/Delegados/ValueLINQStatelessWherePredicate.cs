using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.ValueLINQ.Estados;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Delegados
{
    /// <summary>
    /// Adaptador que envuelve un predicado sin estado para cumplir con la interfaz <see cref="IWhereDelegado{TOrigen, TDato}"/>.
    /// </summary>
    public struct ValueLINQStatelessWherePredicate<TOrigen, TWhereDelegado> : IWhereDelegado<TOrigen, ValueLINQVoidState>
        where TWhereDelegado : struct, IWhereDelegado<TOrigen>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
#endif
    {
        private TWhereDelegado _predicado;

        /// <summary>
        /// Inicializa el adaptador envolviendo el predicado sin estado indicado.
        /// </summary>
        /// <param name="predicado">El predicado sin estado a envolver.</param>
        public ValueLINQStatelessWherePredicate(in TWhereDelegado predicado)
        {
            _predicado = predicado;
        }

        /// <summary>
        /// Evalúa el predicado envuelto sobre el elemento especificado.
        /// </summary>
        /// <param name="objetoLista">El elemento a evaluar.</param>
        /// <param name="otro">El estado vacío del adaptador, sin efecto en la evaluación.</param>
        /// <returns><see langword="true"/> si el elemento cumple la condición; de lo contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Ejecutar(TOrigen objetoLista, ValueLINQVoidState otro)
        {
            return _predicado.Ejecutar(objetoLista);
        }
    }
}
