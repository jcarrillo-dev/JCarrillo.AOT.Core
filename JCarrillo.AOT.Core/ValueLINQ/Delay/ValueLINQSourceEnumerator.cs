#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Enumerador inicial que obtiene los elementos del almacenamiento de origen sin copiarlos.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos del origen.</typeparam>
    public ref struct ValueLINQSourceEnumerator<T> : IValueLINQEnumerator<T>
    {
        private readonly ReadOnlySpan<T> _span;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQSourceEnumerator(ReadOnlySpan<T> span)
        {
            _span = span;
            _index = -1;
        }

        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia al elemento actual de tipo <typeparamref name="T"/>.</value>
        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento del intervalo de origen.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final del intervalo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            bool hasNext = next < _span.Length;
            if (hasNext)
                _index = next;
            else
                Dispose();
            return hasNext;
        }

        /// <summary>
        /// Libera los recursos utilizados por el enumerador.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _index = _span.Length;
        }
    }
}
#endif
