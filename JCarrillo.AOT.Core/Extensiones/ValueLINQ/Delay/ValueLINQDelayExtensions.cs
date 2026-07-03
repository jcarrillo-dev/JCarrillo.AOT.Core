#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Delay;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay
{
    /// <summary>
    /// Métodos de extensión para iniciar la evaluación perezosa (lazy) con Delay.
    /// </summary>
    public static class ValueLINQDelayExtensions
    {
        /// <summary>
        /// Convierte una consulta <see cref="ValueLINQStruct{T}"/> en un flujo de datos de evaluación perezosa (lazy).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en la consulta.</typeparam>
        /// <param name="query">La consulta de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>> ToValueDelayQuery<T>(this ValueLINQStruct<T> query)
        {
            ValueLINQSessionEnumerator<T> sessionEnumerator = new(query.Token);
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator);
        }

        /// <summary>
        /// Convierte una consulta <see cref="ValueLINQRefStruct{T}"/> en un flujo de datos de evaluación perezosa (lazy).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en la consulta.</typeparam>
        /// <param name="query">La consulta de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>> ToValueDelayQuery<T>(this ValueLINQRefStruct<T> query)
        {
            ValueLINQSessionEnumerator<T> sessionEnumerator = new(query.Token);
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator);
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un arreglo estándar de tipo <typeparamref name="T"/>[].
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el arreglo.</typeparam>
        /// <param name="origen">El arreglo de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de origen.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this T[] origen)
        {
            ValueLINQSourceEnumerator<T> sourceEnumerator = new(origen);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator);
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un intervalo de elementos de tipo <see cref="Span{T}"/>.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el intervalo.</typeparam>
        /// <param name="origen">El intervalo de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de origen.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this Span<T> origen)
        {
            ValueLINQSourceEnumerator<T> sourceEnumerator = new(origen);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator);
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un intervalo de solo lectura de tipo <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el intervalo.</typeparam>
        /// <param name="span">El intervalo de solo lectura de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de origen.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this ReadOnlySpan<T> span)
        {
            ValueLINQSourceEnumerator<T> sourceEnumerator = new(span);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator);
        }
    }
}
#endif
