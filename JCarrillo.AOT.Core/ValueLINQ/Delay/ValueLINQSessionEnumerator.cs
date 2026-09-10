#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
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
                _span = arregloLocal.AsSpan(0, tamañoLocal);
            else
                _span = default;

            _index = -1;
            _tokenEsperado = token;
        }

        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia al elemento actual de tipo <typeparamref name="T"/>.</value>
        /// <remarks>
        /// No valida la sesión: hacerlo encarecía el recorrido un 83% por elemento **(medido)** para cerrar únicamente la ventana entre el <see cref="MoveNext"/> que ya la validó y la lectura del elemento. La referencia es de solo lectura y apunta a un <see cref="ReadOnlySpan{T}"/>, así que aprovechar esa ventana exige retener la referencia a propósito más allá de la iteración, cosa que el contrato de un enumerador no contempla.
        /// </remarks>
        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento de la sesión actual.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final de la colección.</returns>
        /// <remarks>
        /// Perder la sesión no termina la enumeración en silencio, sino que lanza: entregar los elementos ya leídos y detenerse produciría un resultado truncado indistinguible de uno completo. Unifica el criterio con el motor eager, cuyo <c>GetEnumerator</c> ya valida el token al abrir.
        /// </remarks>
        /// <exception cref="ValueLinqArenaInactivaException">Se lanza cuando la arena de la sesión fue liberada.</exception>
        /// <exception cref="ValueLinqSesionExpiradaException">Se lanza cuando la sesión ya no es válida.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_tokenEsperado == 0L)
                return false;

            if (!ValueLINQStateManager<T>.IsMetadatoValido(_tokenEsperado))
                ThrowSesionPerdida(_tokenEsperado);

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

        /// <summary>
        /// Lanza distinguiendo si la sesión se perdió porque su arena fue liberada o porque la sesión caducó por su cuenta.
        /// </summary>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSesionPerdida(long token)
        {
            int idArena = TokenHelper.ObtenerArenaId(token);

            if (!ValueLINQArenaManager.IsArenaViva(idArena))
                throw new ValueLinqArenaInactivaException(idArena);

            throw new ValueLinqSesionExpiradaException(token, 0L, TokenHelper.ObtenerSlotIndex(token));
        }
    }
}
#endif
