using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.Diagnostico;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Delegados;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ
{
    /// <summary>
    /// Métodos de extensión ergonómicos para la canalización eager de ValueLINQ.
    /// </summary>
    public static class ValueLINQErgonomicExtensions
    {
        /// <summary>
        /// Filtra un flujo de datos eager basándose en un predicado ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Where<T>(this ValueLINQStruct<T> origen, Func<T, bool> predicate)
        {
            ValueLINQFuncWherePredicate<T> adapter = new();
            return origen.Where(predicate, in adapter);
        }

        /// <summary>
        /// Filtra un flujo de datos eager basándose en un predicado ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Where<T>(this ValueLINQRefStruct<T> origen, Func<T, bool> predicate)
        {
            return origen.Where(predicate, default(ValueLINQFuncWherePredicate<T>));
        }

        /// <summary>
        /// Proyecta cada elemento de un flujo de datos eager en un nuevo formulario utilizando un selector ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<TResultado> Select<T, TResultado>(this ValueLINQStruct<T> origen, Func<T, TResultado> selector)
        {
            ValueLINQFuncSelectSelector<T, TResultado> adapter = new(selector);
            return origen.Select<T, ValueLINQFuncSelectSelector<T, TResultado>, TResultado>(in adapter);
        }

        /// <summary>
        /// Proyecta cada elemento de un flujo de datos eager en un nuevo formulario utilizando un selector ergonómico (delegado <see cref="Func{T, TResult}"/>).
        /// </summary>
        [Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<TResultado> Select<T, TResultado>(this ValueLINQRefStruct<T> origen, Func<T, TResultado> selector)
        {
            var adapter = new ValueLINQFuncSelectSelector<T, TResultado>(selector);
            return origen.Select<T, ValueLINQFuncSelectSelector<T, TResultado>, TResultado>(in adapter);
        }
    }
}
