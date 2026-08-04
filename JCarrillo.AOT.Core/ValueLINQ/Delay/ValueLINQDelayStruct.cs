#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Estructura que representa una consulta de evaluación perezosa (lazy).
    /// </summary>
    public ref struct ValueLINQDelayStruct<T, TEnumerator>
        where T : allows ref struct
        where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
    {
        internal TEnumerator _enumerator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQDelayStruct(TEnumerator enumerator) => _enumerator = enumerator;

        /// <summary>
        /// Filtra un flujo de datos perezoso basándose en una condición (predicado) y un estado adicional de forma eficiente.
        /// </summary>
        /// <typeparam name="TState">El tipo del estado adicional. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{T, TState}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="state">El estado adicional a pasar al predicado.</param>
        /// <param name="predicate">Una referencia al predicado de filtro.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> con el enumerador de filtro aplicado.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>> Where<TPredicate, TState>(TState state, scoped ref TPredicate predicate)
            where TPredicate : struct, IWhereDelegado<T, TState>, allows ref struct
            where TState : allows ref struct
        {
            ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState> whereEnumerator = new(ref _enumerator, ref predicate, state);
            return new ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>>(whereEnumerator);
        }
        /// <summary>
        /// Filtra los elementos del flujo de datos perezoso instanciando un predicado <typeparamref name="TPredicate"/> por defecto (<see langword="default"/>).
        /// </summary>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{T, TState}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TState">El tipo del estado adicional a pasar al predicado. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="state">El estado adicional a pasar al predicado.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> con el enumerador de filtro aplicado.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>> Where<TPredicate, TState>(TState state)
            where TPredicate : struct, IWhereDelegado<T, TState>, allows ref struct
            where TState : allows ref struct
        {
            TPredicate predicate = default;
            ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState> whereEnumerator = new(ref _enumerator, ref predicate, state);
            return new ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>>(whereEnumerator);
        }


        /// <summary>
        /// Proyecta cada elemento de un flujo de datos perezoso en un nuevo formulario utilizando un selector eficiente.
        /// </summary>
        /// <typeparam name="TSelector">El tipo del selector que implementa <see cref="ISelectDelegado{T, TResultado}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento proyectado resultante. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="selector">Una referencia al selector de proyección.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{TResultado, TEnumerator}"/> con el enumerador de proyección aplicado.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>> Select<TSelector, TResultado>(scoped ref TSelector selector)
            where TSelector : struct, ISelectDelegado<T, TResultado>, allows ref struct
            where TResultado : allows ref struct
        {
            ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T> selectEnumerator = new(ref _enumerator, ref selector);
            return new ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>>(selectEnumerator);
        }

        /// <summary>
        /// Proyecta cada elemento de un flujo de datos perezoso en un nuevo formulario instanciando un selector <typeparamref name="TSelector"/> por defecto (<see langword="default"/>).
        /// </summary>
        /// <typeparam name="TSelector">El tipo del selector que implementa <see cref="ISelectDelegado{T, TResultado}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento proyectado resultante. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{TResultado, TEnumerator}"/> con el enumerador de proyección aplicado.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>> Select<TSelector, TResultado>()
            where TSelector : struct, ISelectDelegado<T, TResultado>, allows ref struct
            where TResultado : allows ref struct
        {
            TSelector newSelect = default;
            ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T> selectEnumerator = new(ref _enumerator, ref newSelect);
            return new ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>>(selectEnumerator);
        }

        /// <summary>
        /// Obtiene el enumerador subyacente para recorrer los elementos del flujo de datos perezoso.
        /// </summary>
        /// <returns>El enumerador subyacente de tipo <typeparamref name="TEnumerator"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TEnumerator GetEnumerator() => _enumerator;
    }
}
#endif
