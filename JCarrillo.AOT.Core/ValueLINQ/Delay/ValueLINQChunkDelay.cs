#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Enumerador estructurado perezoso (lazy) de tipo <see langword="ref struct"/> que agrupa los elementos generados por un flujo en fragmentos de tamaño fijo (<see cref="ReadOnlySpan{T}"/>).
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos del flujo.</typeparam>
    /// <typeparam name="TEnumerator">El tipo del enumerador subyacente. Debe implementar <see cref="IValueLINQEnumerator{T}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
    public ref struct ValueLINQChunkDelay<T, TEnumerator> : IValueLINQEnumerator<ReadOnlySpan<T>>
        where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
    {
        private TEnumerator _enumerator;

        private readonly int _chunkSize;
        private readonly ref MetadatosSesion<T> _metadatos;
        private long _token;
        private ReadOnlySpan<T> _currentSpan;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQChunkDelay(scoped ref TEnumerator enumerator, int chunkSize, ValueLINQDelayOptions opciones)
        {
            if (chunkSize <= 0)
                ThrowArgumentOutOfRangeException();

            _enumerator = enumerator;

            _chunkSize = chunkSize;
            _metadatos = ref ObtenerMetadatos(chunkSize, opciones);
            _token = TokenHelper.LeerToken(ref _metadatos.Token);
            _currentSpan = default;
        }

        /// <summary>
        /// Obtiene una referencia de solo lectura al fragmento (<see cref="ReadOnlySpan{T}"/>) en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia de solo lectura al fragmento actual de elementos.</value>
        /// <exception cref="ValueLinqArenaInactivaException">Se lanza cuando la arena del buffer fue liberada.</exception>
        /// <exception cref="ValueLinqSesionExpiradaException">Se lanza cuando la sesión del buffer ya no es válida.</exception>
        public readonly ref readonly ReadOnlySpan<T> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_token != 0L && !ValueLINQStateManager<T>.IsMetadatoValido(_token))
                    ThrowSesionPerdida(_token);

                return ref Unsafe.AsRef(in _currentSpan);
            }
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente bloque de datos agrupados del flujo.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador avanzó con éxito al siguiente fragmento; <see langword="false"/> si se alcanzó el final del flujo.</returns>
        /// <remarks>
        /// Perder la sesión del buffer no termina la enumeración en silencio, sino que lanza: entregar los fragmentos ya leídos y detenerse produciría un resultado truncado indistinguible de uno completo.
        /// </remarks>
        /// <exception cref="ValueLinqArenaInactivaException">Se lanza cuando la arena del buffer fue liberada.</exception>
        /// <exception cref="ValueLinqSesionExpiradaException">Se lanza cuando la sesión del buffer ya no es válida.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_token == 0L)
            {
                _currentSpan = default;
                return false;
            }

            if (!ValueLINQStateManager<T>.IsMetadatoValido(_token))
                ThrowSesionPerdida(_token);

            ValueLINQStateManager<T>.RefrescarUltimoAcceso(_token, ValueLINQConfig.TiempoRefrescoAcceso);

            T[]? array = Volatile.Read(ref _metadatos.Array);
            if (array == null)
                ThrowSesionPerdida(_token);

            int tamaño = 0;

            while (_enumerator.MoveNext())
            {
                array[tamaño++] = _enumerator.Current;
                if (tamaño >= _chunkSize)
                {
                    _currentSpan = array.AsSpan(0, tamaño);
                    ValueLINQStateManager<T>.RefrescarUltimoAcceso(_token, ValueLINQConfig.TiempoRefrescoAcceso);
                    return true;
                }
            }

            if (tamaño > 0)
            {
                _currentSpan = array.AsSpan(0, tamaño);
                ValueLINQStateManager<T>.RefrescarUltimoAcceso(_token, ValueLINQConfig.TiempoRefrescoAcceso);
                return true;
            }

            _currentSpan = default;
            Dispose();
            return false;
        }

        /// <summary>
        /// Libera los recursos asociados a la instancia del enumerador, incluyendo el enumerador de origen y la sesión del buffer agrupado.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_token == 0L)
                return;

            DisposeSlow();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeSlow()
        {
            long token = _token;
            _token = 0L;
            _currentSpan = default;

            try
            {
                _enumerator.Dispose();
                _enumerator = default!;
            }
            finally
            {
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref MetadatosSesion<T> ObtenerMetadatos(int chunkSize, ValueLINQDelayOptions opciones)
        {
            if (!opciones.HasArena)
                return ref ValueLINQStateManager<T>.ObtenerMetadatos(chunkSize);

            if (!ValueLINQArenaManager.IsArenaViva(opciones.TokenArena))
                ThrowArenaInactiva(opciones.IdArena);

            return ref ValueLINQStateManager<T>.ObtenerMetadatos(opciones.IdArena, chunkSize);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArenaInactiva(int idArena)
            => throw new ValueLinqArenaInactivaException(idArena);

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

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRangeException()
            => throw new ArgumentOutOfRangeException("chunkSize", "El tamaño del chunk debe ser mayor que cero.");
    }
}
#endif
