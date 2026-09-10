#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Delegados;
using JCarrillo.AOT.Core.ValueLINQ.Estados;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
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

        internal ValueLINQDelayOptions _opciones;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQDelayStruct(TEnumerator enumerator, ValueLINQDelayOptions opciones)
        {
            _enumerator = enumerator;
            _opciones = opciones;
        }

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
            return new ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>>(whereEnumerator, _opciones);
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
            return new ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>>(whereEnumerator, _opciones);
        }

        /// <summary>
        /// Filtra un flujo de datos perezoso basándose en un predicado struct sin estado.
        /// </summary>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{T}"/>. Debe ser una estructura y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="predicate">El predicado de filtro sin estado.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> con el enumerador de filtro aplicado.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, ValueLINQStatelessWherePredicate<T, TPredicate>, ValueLINQVoidState>> Where<TPredicate>(in TPredicate predicate)
            where TPredicate : struct, IWhereDelegado<T>
        {
            ValueLINQStatelessWherePredicate<T, TPredicate> adapter = new(in predicate);
            return Where<ValueLINQStatelessWherePredicate<T, TPredicate>, ValueLINQVoidState>(default, ref adapter);
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
            return new ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>>(selectEnumerator, _opciones);
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
            return new ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>>(selectEnumerator, _opciones);
        }

        #region Concat

        /// <summary>
        /// Concatena esta consulta con otra, emitiendo primero todos los elementos de esta y después los de la segunda.
        /// </summary>
        /// <typeparam name="TSegundo">El tipo del enumerador de la segunda consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="segundaConsulta">La consulta cuyos elementos se emiten a continuación de los de esta.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> que recorre ambas consultas en orden.</returns>
        /// <exception cref="ValueLinqArenaCruzadaException">Se lanza cuando ambas consultas viven en arenas distintas.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>> Concat<TSegundo>(ValueLINQDelayStruct<T, TSegundo> segundaConsulta)
            where TSegundo : IValueLINQEnumerator<T>, allows ref struct
        {
            _opciones.ValidarArenaUnica(segundaConsulta._opciones);

            ValueLINQConcatDelay<T, TEnumerator, TSegundo> concatRaiz = new(ref _enumerator, ref segundaConsulta._enumerator);
            return new ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>>(concatRaiz, _opciones);
        }

        /// <summary>
        /// Concatena esta consulta con otras dos, emitiendo sus elementos en el orden de los parámetros.
        /// </summary>
        /// <typeparam name="TSegundo">El tipo del enumerador de la segunda consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TTercero">El tipo del enumerador de la tercera consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="segundaConsulta">La segunda consulta del recorrido.</param>
        /// <param name="terceraConsulta">La tercera consulta del recorrido.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> que recorre las tres consultas en orden.</returns>
        /// <remarks>
        /// El receptor cuenta como fuente, de modo que esta sobrecarga concatena tres. Construye el árbol
        /// <c>C(C(A, B), C)</c>, de profundidad dos frente a la misma profundidad del encadenamiento binario.
        /// </remarks>
        /// <exception cref="ValueLinqArenaCruzadaException">Se lanza cuando alguna consulta vive en una arena distinta de la de esta.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, TTercero>> Concat<TSegundo, TTercero>(ValueLINQDelayStruct<T, TSegundo> segundaConsulta, ValueLINQDelayStruct<T, TTercero> terceraConsulta)
            where TSegundo : IValueLINQEnumerator<T>, allows ref struct
            where TTercero : IValueLINQEnumerator<T>, allows ref struct
        {
            _opciones.ValidarArenaUnica(segundaConsulta._opciones, terceraConsulta._opciones);

            ValueLINQConcatDelay<T, TEnumerator, TSegundo> concatIzquierda = new(ref _enumerator, ref segundaConsulta._enumerator);
            ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, TTercero> concatRaiz = new(ref concatIzquierda, ref terceraConsulta._enumerator);
            return new ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, TTercero>>(concatRaiz, _opciones);
        }

        /// <summary>
        /// Concatena esta consulta con otras tres, emitiendo sus elementos en el orden de los parámetros.
        /// </summary>
        /// <typeparam name="TSegundo">El tipo del enumerador de la segunda consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TTercero">El tipo del enumerador de la tercera consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TCuarto">El tipo del enumerador de la cuarta consulta. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="segundaConsulta">La segunda consulta del recorrido.</param>
        /// <param name="terceraConsulta">La tercera consulta del recorrido.</param>
        /// <param name="cuartaConsulta">La cuarta consulta del recorrido.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> que recorre las cuatro consultas en orden.</returns>
        /// <remarks>
        /// El receptor cuenta como fuente, de modo que esta sobrecarga concatena cuatro. Construye el árbol equilibrado
        /// <c>C(C(A, B), C(C, D))</c>, de profundidad dos frente a la profundidad tres del encadenamiento binario;
        /// a partir de cuatro fuentes conviene preferir esta forma a encadenar <c>Concat</c> uno tras otro, porque la
        /// profundidad del anidamiento acaba topando con el límite de inlining del JIT. Como la concatenación es
        /// asociativa, equilibrar el árbol cambia la profundidad pero nunca el orden de emisión, y componiendo esta
        /// sobrecarga con la binaria se obtienen árboles equilibrados de cualquier tamaño:
        /// <c>a.Concat(b, c, d).Concat(e.Concat(f, g, h))</c> recorre ocho fuentes con profundidad tres.
        /// </remarks>
        /// <exception cref="ValueLinqArenaCruzadaException">Se lanza cuando alguna consulta vive en una arena distinta de la de esta.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, ValueLINQConcatDelay<T, TTercero, TCuarto>>> Concat<TSegundo, TTercero, TCuarto>(ValueLINQDelayStruct<T, TSegundo> segundaConsulta, ValueLINQDelayStruct<T, TTercero> terceraConsulta, ValueLINQDelayStruct<T, TCuarto> cuartaConsulta)
            where TSegundo : IValueLINQEnumerator<T>, allows ref struct
            where TTercero : IValueLINQEnumerator<T>, allows ref struct
            where TCuarto : IValueLINQEnumerator<T>, allows ref struct
        {
            _opciones.ValidarArenaUnica(segundaConsulta._opciones, terceraConsulta._opciones, cuartaConsulta._opciones);

            ValueLINQConcatDelay<T, TEnumerator, TSegundo> concatIzquierda = new(ref _enumerator, ref segundaConsulta._enumerator);
            ValueLINQConcatDelay<T, TTercero, TCuarto> concatDerecha = new(ref terceraConsulta._enumerator, ref cuartaConsulta._enumerator);
            ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, ValueLINQConcatDelay<T, TTercero, TCuarto>> concatRaiz = new(ref concatIzquierda, ref concatDerecha);
            return new ValueLINQDelayStruct<T, ValueLINQConcatDelay<T, ValueLINQConcatDelay<T, TEnumerator, TSegundo>, ValueLINQConcatDelay<T, TTercero, TCuarto>>>(concatRaiz, _opciones);
        }

        #endregion

        /// <summary>
        /// Obtiene el enumerador subyacente para recorrer los elementos del flujo de datos perezoso.
        /// </summary>
        /// <returns>El enumerador subyacente de tipo <typeparamref name="TEnumerator"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly TEnumerator GetEnumerator() => _enumerator;
    }
}
#endif
