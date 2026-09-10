#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Combinador que proyecta elementos de forma perezosa mediante un selector struct.
    /// </summary>
    /// <typeparam name="TResultado">El tipo del elemento de resultado. Admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TEnumerator">El tipo del enumerador subyacente. Debe implementar <see cref="IValueLINQEnumerator{TOrigen}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TSelector">El tipo del selector que implementa <see cref="ISelectDelegado{TOrigen, TResultado}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
    /// <typeparam name="TOrigen">El tipo de origen de los elementos. Admite estructuras de referencia (allows ref struct).</typeparam>
    public ref struct ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, TOrigen> : IValueLINQEnumerator<TResultado>
        where TResultado : allows ref struct
        where TOrigen : allows ref struct
        where TEnumerator : IValueLINQEnumerator<TOrigen>, allows ref struct
        where TSelector : struct, ISelectDelegado<TOrigen, TResultado>, allows ref struct
    {
        private TEnumerator _enumerator;
        private readonly TSelector _selector;
        private TResultado _current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQSelectDelay(scoped ref TEnumerator enumerator, scoped ref TSelector selector)
        {
            _enumerator = enumerator;
            _selector = selector;
            _current = default!;
        }

        /// <summary>
        /// Obtiene una referencia de solo lectura al elemento en la posición actual del enumerador.
        /// </summary>
        /// <value>Una referencia al elemento transformado actual de tipo <typeparamref name="TResultado"/>.</value>
        public readonly ref readonly TResultado Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef(in _current);
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento del flujo transformado.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final del flujo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (_enumerator is null)
                return false;

            if (_enumerator.MoveNext())
            {
                _current = _selector.Ejecutar(_enumerator.Current);
                return true;
            }

            Dispose();
            return false;
        }

        /// <summary>
        /// Libera los recursos utilizados por el enumerador de origen.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_enumerator is not null)
            {
                _enumerator.Dispose();
                _enumerator = default!;
            }
        }
    }
}
#endif
