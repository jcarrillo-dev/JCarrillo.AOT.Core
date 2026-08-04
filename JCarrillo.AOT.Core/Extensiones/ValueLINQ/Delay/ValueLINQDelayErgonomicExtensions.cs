#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Diagnostico;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delegados;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay
{
    /// <summary>
    /// Métodos de extensión ergonómicos para la canalización de evaluación perezosa (lazy).
    /// </summary>
    public static class ValueLINQDelayErgonomicExtensions
    {
        #region Where

        /// <summary>
        /// Filtra un flujo de datos perezoso basándose en un predicado ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso de origen.</param>
        /// <param name="predicate">La función para probar cada elemento en busca de una condición.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> que contiene los elementos que cumplen la condición.</returns>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, ValueLINQFuncWherePredicate<T>, Func<T, bool>>> Where<T, TEnumerator>(
            this ValueLINQDelayStruct<T, TEnumerator> pipeline,
            Func<T, bool> predicate)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            ValueLINQFuncWherePredicate<T> adapter = new();
            return pipeline.Where(predicate, ref adapter);
        }

        #endregion

        #region Select

        /// <summary>
        /// Proyecta cada elemento de un flujo de datos perezoso en un nuevo formulario utilizando un selector ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TResultado">El tipo del valor devuelto por <paramref name="selector"/>.</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso de origen.</param>
        /// <param name="selector">Una función de transformación que se aplica a cada elemento.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TResultado}"/> cuyos elementos son el resultado de invocar la función de transformación en cada elemento de origen.</returns>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, ValueLINQFuncSelectSelector<T, TResultado>, T>> Select<T, TEnumerator, TResultado>(
            this ValueLINQDelayStruct<T, TEnumerator> pipeline,
            Func<T, TResultado> selector)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            ValueLINQFuncSelectSelector<T, TResultado> adapter = new(selector);
            return pipeline.Select<ValueLINQFuncSelectSelector<T, TResultado>, TResultado>(ref adapter);
        }

        #endregion

        #region Chunk

        /// <summary>
        /// Define un delegado ergonómico diseñado para ejecutar procesamiento arbitrario sobre un fragmento por referencia scoped.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos contenidos en el fragmento.</typeparam>
        /// <param name="chunk">La referencia al fragmento expuesto como <see cref="ReadOnlySpan{T}"/>.</param>
        public delegate void ProcesarChunkDelegado<T>(scoped ref ReadOnlySpan<T> chunk);

        /// <summary>
        /// Consume de forma terminal la canalización de fragmentos de forma ergonómica mediante una expresión lambda o delegado tradicional.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos contenidos en los fragmentos.</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">La canalización perezosa que genera los fragmentos.</param>
        /// <param name="procesar">La lambda o delegado ergonómico que procesará cada fragmento de forma síncrona.</param>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcesarChunk<T, TEnumerator>(
            this ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline,
            ProcesarChunkDelegado<T> procesar)
            where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
            => ValueLINQDelayExtensions.ProcesarChunkRef(pipeline, new ValueLINQFuncProcesarChunk<T>(procesar));

        #endregion
    }
}
#endif
