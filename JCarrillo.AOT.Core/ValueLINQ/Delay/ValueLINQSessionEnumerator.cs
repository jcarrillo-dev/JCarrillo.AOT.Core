#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Enumerador que obtiene los elementos diferidos a partir de un token de sesión y valida la sesión en MoveNext.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos en la sesión.</typeparam>
    public ref struct ValueLINQSessionEnumerator<T> : IValueLINQEnumerator<T>
    {
        private readonly long _token;
        private ReadOnlySpan<T> _span;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQSessionEnumerator(long token)
        {
            _token = token;
            _span = default;
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
        /// Desplaza el enumerador al siguiente elemento de la sesión actual.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final de la colección.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(_token);
            _span = metadatos.Array.AsSpan(0, metadatos.TamañoActual);

            int next = _index + 1;
            bool hasNext = next < _span.Length;
            if (hasNext)
                _index = next;
            return hasNext;
        }

        /// <summary>
        /// Libera los recursos de sesión asociados a esta consulta en el administrador de estados.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose()
        {
        }
    }
}
#endif
