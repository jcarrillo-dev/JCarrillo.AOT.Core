using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Delegados
{
    /// <summary>
    /// Adaptador para predicados basados en delegados Func.
    /// </summary>
    public readonly struct ValueLINQFuncWherePredicate<T> : IWhereDelegado<T, Func<T, bool>>
#if NET9_0_OR_GREATER
        where T : allows ref struct
#endif
    {
        /// <summary>
        /// Evalúa si el elemento cumple con la condición definida por el delegado de estado especificado.
        /// </summary>
        /// <param name="item">El elemento a evaluar.</param>
        /// <param name="estado">La función de predicado ergonómica.</param>
        /// <returns><see langword="true"/> si el elemento cumple con la condición; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Ejecutar(T item, Func<T, bool> estado) => estado(item);
    }
}
