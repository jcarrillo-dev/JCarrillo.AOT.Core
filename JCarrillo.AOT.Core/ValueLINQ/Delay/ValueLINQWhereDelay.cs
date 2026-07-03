#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Combinador que filtra elementos de forma perezosa mediante un predicado struct.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TEnumerator">El tipo del enumerador subyacente. Debe implementar <see cref="IValueLINQEnumerator{T}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{T, TState}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TState">El tipo del estado adicional. Admite estructuras de referencia (allows ref struct).</typeparam>
    public ref struct ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState> : IValueLINQEnumerator<T>
        where T : allows ref struct
        where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        where TPredicate : struct, IWhereDelegado<T, TState>, allows ref struct
        where TState : allows ref struct
    {
        private TEnumerator _enumerator;
        private readonly TPredicate _predicate;
        private readonly TState _state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQWhereDelay(scoped ref TEnumerator enumerator, scoped ref TPredicate predicate, TState state)
        {
            _enumerator = enumerator;
            _predicate = predicate;
            _state = state;
        }

        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia al elemento actual de tipo <typeparamref name="T"/>.</value>
        public readonly ref readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _enumerator.Current;
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento que cumpla con la condición del predicado.
        /// </summary>
        /// <returns><see langword="true"/> si se encontró un elemento que cumple la condición y el enumerador se desplazó; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            while (_enumerator.MoveNext())
            {
                if (_predicate.Ejecutar(_enumerator.Current, _state))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Libera los recursos del enumerador de origen.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _enumerator.Dispose();
            _enumerator = default!;
        }
    }
}
#endif
