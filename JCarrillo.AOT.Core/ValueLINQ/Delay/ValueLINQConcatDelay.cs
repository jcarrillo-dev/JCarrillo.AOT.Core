#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Combinador que recorre de forma perezosa dos enumeradores en secuencia, agotando el primero antes de pasar al segundo.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos del flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TEnumerator1">El tipo del primer enumerador. Debe implementar <see cref="IValueLINQEnumerator{T}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TEnumerator2">El tipo del segundo enumerador. Debe implementar <see cref="IValueLINQEnumerator{T}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <remarks>
    /// Los dos enumeradores tienen parámetros de tipo independientes para poder concatenar canalizaciones heterogéneas,
    /// por ejemplo el resultado de un <c>Where</c> con el de un <c>Select</c>. Concatenar más de dos fuentes se consigue
    /// anidando este combinador; como la concatenación es asociativa, la forma del árbol solo altera la profundidad.
    /// </remarks>
    public ref struct ValueLINQConcatDelay<T, TEnumerator1, TEnumerator2> : IValueLINQEnumerator<T>
        where T : allows ref struct
        where TEnumerator1 : IValueLINQEnumerator<T>, allows ref struct
        where TEnumerator2 : IValueLINQEnumerator<T>, allows ref struct
    {
        private TEnumerator1 _primerEnumerator;
        private TEnumerator2 _segundoEnumerator;

        private bool _enPrimerEnumerator;
        private bool _disposed;

        /// <summary>
        /// Inicializa una nueva instancia del combinador a partir de los dos enumeradores que se recorrerán en secuencia.
        /// </summary>
        /// <param name="primerEnumerator">El enumerador que se agota primero.</param>
        /// <param name="segundoEnumerator">El enumerador que continúa cuando el primero se agota.</param>
        public ValueLINQConcatDelay(scoped ref TEnumerator1 primerEnumerator, scoped ref TEnumerator2 segundoEnumerator)
        {
            _primerEnumerator = primerEnumerator;
            _segundoEnumerator = segundoEnumerator;

            _enPrimerEnumerator = true;
            _disposed = false;
        }
        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador activo.
        /// </summary>
        /// <value>Una referencia de solo lectura al elemento actual de tipo <typeparamref name="T"/>.</value>
        public ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _enPrimerEnumerator ? ref _primerEnumerator.Current : ref _segundoEnumerator.Current;
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento, pasando del primer enumerador al segundo cuando aquel se agota.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si ambos enumeradores se agotaron.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_enPrimerEnumerator)
                if (MoveNextEnumerator(ref _primerEnumerator))
                    return true;
                else
                    _enPrimerEnumerator = false;

            if (MoveNextEnumerator(ref _segundoEnumerator))
                return true;

            Dispose();

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool MoveNextEnumerator<TEnumerator>(scoped ref TEnumerator enumerator)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
            => !_disposed && enumerator.MoveNext();

        /// <summary>
        /// Libera ambos enumeradores subyacentes, propagando la disposición en cadena.
        /// </summary>
        /// <remarks>
        /// Es idempotente: se invoca automáticamente al agotar el flujo desde <see cref="MoveNext"/> y admite
        /// invocaciones posteriores del consumidor sin volver a liberar los enumeradores.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeSlow();
        }

        /// <summary>
        /// Concentra el camino con manejo de excepciones (try/finally) para no bloquear el inlining del wrapper <see cref="Dispose"/> en net9.0 (el JIT ignora AggressiveInlining con EH) y net10.0 (inline de EH posible pero no garantizado).
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeSlow()
        {
            _disposed = true;

            try
            {
                _primerEnumerator.Dispose();
                _primerEnumerator = default!;
            }
            finally
            {
                _segundoEnumerator.Dispose();
                _segundoEnumerator = default!;
            }
        }
    }
}
#endif