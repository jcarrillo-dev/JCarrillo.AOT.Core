#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Threading;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Enumerador que obtiene los elementos diferidos a partir de un token de sesión y valida la sesión en MoveNext.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos en la sesión.</typeparam>
    public ref struct ValueLINQSessionEnumerator<T> : IValueLINQEnumerator<T>
    {
        private ReadOnlySpan<T> _span;
        private int _index;
        private long _tokenEsperado;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQSessionEnumerator(long token)
        {
            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
            T[]? arregloLocal = Volatile.Read(ref metadatos.Array);
            int tamañoLocal = Volatile.Read(ref metadatos.TamañoActual);

            if (arregloLocal != null && tamañoLocal > 0 && tamañoLocal <= arregloLocal.Length)
            {
                _span = arregloLocal.AsSpan(0, tamañoLocal);
            }
            else
            {
                _span = default;
            }

            _index = -1;
            _tokenEsperado = token;
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
            if (_tokenEsperado == 0L || !ValueLINQStateManager<T>.IsMetadatoValido(_tokenEsperado))
                return false;

            int siguiente = _index + 1;
            _index = siguiente;
            bool hasSiguiente = (uint)siguiente < (uint)_span.Length;

            if (!hasSiguiente)
            {
                Dispose();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Libera los recursos de sesión asociados a esta consulta en el administrador de estados.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_tokenEsperado == 0L)
                return;

            long token = _tokenEsperado;
            _tokenEsperado = 0L;
            ValueLINQStateManager<T>.LiberarMetadatos(token);
        }
    }
}
#endif
