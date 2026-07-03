#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.Diagnostico;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delegados;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay
{
    /// <summary>
    /// Métodos de extensión ergonómicos para la canalización de evaluación perezosa (lazy).
    /// </summary>
    public static class ValueLINQDelayErgonomicExtensions
    {
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
            return pipeline.Select<T, ValueLINQFuncSelectSelector<T, TResultado>, TResultado>(ref adapter);
        }
    }
}
#endif
