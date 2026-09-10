#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay
{
    /// <summary>
    /// Métodos de extensión para iniciar la evaluación perezosa (lazy) con Delay.
    /// </summary>
    public static class ValueLINQDelayExtensions
    {
        #region ToValueDelayQuery

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
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator, ValueLINQDelayOptions.Ambiente.DesdeSesion(query.Token));
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
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator, ValueLINQDelayOptions.Ambiente.DesdeSesion(query.Token));
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un arreglo estándar de tipo <typeparamref name="T"/>[].
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el arreglo.</typeparam>
        /// <param name="origen">El arreglo de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> configurada con un enumerador de origen.</returns>
        /// <exception cref="ArgumentNullException">
        /// Se lanza cuando <paramref name="origen"/> es <see langword="null"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this T[] origen)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQSourceEnumerator<T> sourceEnumerator = new(origen);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente);
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
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente);
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
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente);
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un arreglo, cuyas sesiones intermedias vivirán en la arena indicada.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el arreglo.</typeparam>
        /// <param name="origen">El arreglo de origen.</param>
        /// <param name="arena">La arena propietaria de las sesiones que cree la canalización.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> adscrita a la arena.</returns>
        /// <remarks>
        /// Los operadores que reservan memoria (como <c>Chunk</c>) pedirán su buffer a esta arena, de modo que disponerla
        /// libera esos buffers aunque la canalización no llegue a disponerse.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Se lanza cuando <paramref name="origen"/> es <see langword="null"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this T[] origen, ValueLINQArena arena)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQSourceEnumerator<T> sourceEnumerator = new(origen);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente.DesdeArena(arena));
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un <see cref="Span{T}"/>, cuyas sesiones intermedias vivirán en la arena indicada.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el intervalo.</typeparam>
        /// <param name="origen">El intervalo de origen.</param>
        /// <param name="arena">La arena propietaria de las sesiones que cree la canalización.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> adscrita a la arena.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this Span<T> origen, ValueLINQArena arena)
        {
            ValueLINQSourceEnumerator<T> sourceEnumerator = new(origen);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente.DesdeArena(arena));
        }

        /// <summary>
        /// Crea un flujo de datos de evaluación perezosa (lazy) a partir de un <see cref="ReadOnlySpan{T}"/>, cuyas sesiones intermedias vivirán en la arena indicada.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el intervalo.</typeparam>
        /// <param name="span">El intervalo de solo lectura de origen.</param>
        /// <param name="arena">La arena propietaria de las sesiones que cree la canalización.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, TEnumerator}"/> adscrita a la arena.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>> ToValueDelayQuery<T>(this ReadOnlySpan<T> span, ValueLINQArena arena)
        {
            ValueLINQSourceEnumerator<T> sourceEnumerator = new(span);
            return new ValueLINQDelayStruct<T, ValueLINQSourceEnumerator<T>>(sourceEnumerator, ValueLINQDelayOptions.Ambiente.DesdeArena(arena));
        }

        #endregion

        #region Helpers de Excepciones (Evitan contaminación del JIT)

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentNullException(string paramName)
            => throw new ArgumentNullException(paramName);

        #endregion

        #region Chunk

        /// <summary>
        /// Agrupa los elementos de la canalización perezosa actual en bloques (chunks) de un tamaño máximo especificado.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos de origen.</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">La canalización perezosa de origen.</param>
        /// <param name="chunkSize">El tamaño máximo de cada fragmento.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{TResultado, TEnumerator}"/> que expone de forma perezosa los fragmentos como <see cref="ReadOnlySpan{T}"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<ReadOnlySpan<T>, ValueLINQChunkDelay<T, TEnumerator>> Chunk<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> pipeline, int chunkSize)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            ValueLINQChunkDelay<T, TEnumerator> chunkEnumerator = new(ref pipeline._enumerator, chunkSize, pipeline._opciones);
            return new ValueLINQDelayStruct<ReadOnlySpan<T>, ValueLINQChunkDelay<T, TEnumerator>>(chunkEnumerator, pipeline._opciones);
        }

        /// <summary>
        /// Consume de forma terminal la canalización de fragmentos y los procesa de manera síncrona mediante un procesador estructural mutable pasado por referencia.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos contenidos en los fragmentos.</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TProcesador">El tipo de la estructura del procesador que implementa <see cref="IProcesarChunkRefDelegado{T}"/>.</typeparam>
        /// <param name="pipeline">La canalización perezosa que genera los fragmentos.</param>
        /// <param name="procesar">Una referencia al procesador mutador que ejecutará el procesamiento de cada fragmento.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcesarChunk<T, TEnumerator, TProcesador>(this scoped in ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline, ref TProcesador procesar)
            where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
            where TProcesador : struct, IProcesarChunkRefDelegado<T>
        {
            TEnumerator enumerator = pipeline._enumerator;

            try
            {
                while (enumerator.MoveNext()) procesar.Ejecutar(enumerator.Current);
            }
            finally
            {
                enumerator.Dispose();
            }

        }

        /// <summary>
        /// Consume de forma terminal la canalización de fragmentos y los procesa de manera síncrona mediante un procesador estructural que puede ser un ref struct pasado por valor.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos contenidos en los fragmentos.</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TProcesador">El tipo del procesador que implementa <see cref="IProcesarChunkRefDelegado{T}"/> y admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">La canalización perezosa que genera los fragmentos.</param>
        /// <param name="procesar">El procesador que ejecutará el procesamiento de cada fragmento (puede ser un ref struct temporario).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcesarChunkRef<T, TEnumerator, TProcesador>(this scoped in ValueLINQDelayStruct<ReadOnlySpan<T>, TEnumerator> pipeline, TProcesador procesar)
            where TEnumerator : IValueLINQEnumerator<ReadOnlySpan<T>>, allows ref struct
            where TProcesador : IProcesarChunkRefDelegado<T>, allows ref struct
        {
            TEnumerator enumerator = pipeline._enumerator;

            try
            {
                while (enumerator.MoveNext()) procesar.Ejecutar(enumerator.Current);
            }
            finally
            {
                enumerator.Dispose();
            }

        }

        #endregion
    }
}
#endif
