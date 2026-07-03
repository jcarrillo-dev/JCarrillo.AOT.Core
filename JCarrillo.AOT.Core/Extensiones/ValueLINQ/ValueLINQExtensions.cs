using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Diagnostico;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if !NET9_0_OR_GREATER
using System.ComponentModel;
#endif
#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ
{
    /// <summary>
    /// Provides extension methods for performing high-performance, zero-allocation value-based LINQ operations.
    /// </summary>
    public static partial class ValueLINQExtensions
    {
        #region ToValueQuery

        /// <summary>
        /// Converts an array to a <see cref="ValueLINQStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the array.</typeparam>
        /// <param name="origen">The source array.</param>
        /// <returns>A <see cref="ValueLINQStruct{T}"/> containing the elements of the array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this T[] origen)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.AsSpan(0, origen.Length).CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts an array to a <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the array.</typeparam>
        /// <param name="origen">The source array.</param>
        /// <returns>A <see cref="ValueLINQRefStruct{T}"/> containing the elements of the array.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this T[] origen)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQRefStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.AsSpan(0, origen.Length).CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a span to a <see cref="ValueLINQStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        /// <param name="origen">The source span.</param>
        /// <returns>A <see cref="ValueLINQStruct{T}"/> containing the elements of the span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this Span<T> origen)
        {
            ValueLINQStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a span to a <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        /// <param name="origen">The source span.</param>
        /// <returns>A <see cref="ValueLINQRefStruct{T}"/> containing the elements of the span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this Span<T> origen)
        {
            ValueLINQRefStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a read-only span to a <see cref="ValueLINQStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the read-only span.</typeparam>
        /// <param name="origen">The source read-only span.</param>
        /// <returns>A <see cref="ValueLINQStruct{T}"/> containing the elements of the read-only span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ReadOnlySpan<T> origen)
        {
            ValueLINQStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a read-only span to a <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the read-only span.</typeparam>
        /// <param name="origen">The source read-only span.</param>
        /// <returns>A <see cref="ValueLINQRefStruct{T}"/> containing the elements of the read-only span.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ReadOnlySpan<T> origen)
        {
            ValueLINQRefStruct<T> query = new(origen.Length);
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
                origen.CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
                return query;
            }
            catch
            {
                query.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts a memory reference to a <see cref="ValueLINQStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the memory.</typeparam>
        /// <param name="origen">The source memory reference.</param>
        /// <returns>A <see cref="ValueLINQStruct{T}"/> containing the elements of the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ref Memory<T> origen)
            => origen.Span.ToValueQuery();

        /// <summary>
        /// Converts a memory reference to a <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the memory.</typeparam>
        /// <param name="origen">The source memory reference.</param>
        /// <returns>A <see cref="ValueLINQRefStruct{T}"/> containing the elements of the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref Memory<T> origen)
            => origen.Span.ToValueRefQuery();

        /// <summary>
        /// Converts a pooled list reference to a <see cref="ValueLINQStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the pooled list.</typeparam>
        /// <param name="origen">The source pooled list reference.</param>
        /// <returns>A <see cref="ValueLINQStruct{T}"/> containing the elements of the pooled list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ref PooledList<T> origen)
            => origen.Span.ToValueQuery();

        /// <summary>
        /// Converts a pooled list reference to a <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the pooled list.</typeparam>
        /// <param name="origen">The source pooled list reference.</param>
        /// <returns>A <see cref="ValueLINQRefStruct{T}"/> containing the elements of the pooled list.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref PooledList<T> origen)
            => origen.Span.ToValueRefQuery();

        #endregion

        #region Where

        /// <summary>
        /// Filtra un flujo de datos representado por una estructura de referencia de ValueLINQ basándose en un predicado struct.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TDato">El tipo del dato de comparación.</typeparam>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{TOrigen, TDato}"/>.</typeparam>
        /// <param name="origen">La estructura de origen.</param>
        /// <param name="dato">El valor del dato de comparación.</param>
        /// <param name="predicado">El predicado de filtro.</param>
        /// <returns>Una estructura de referencia de ValueLINQ con los elementos filtrados.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
            this ValueLINQRefStruct<TOrigen> origen,
            TDato dato,
            in TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            ValueLINQRefStruct<TOrigen> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<TOrigen>(origenTamaño);
                if (isTokenValido)
                {
                    ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<TOrigen> metadatosDestino = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(destino.Token);
                        TOrigen[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;
                        TPredicate localPredicate = predicado;

                        for (int i = 0; i < len; i++)
                        {
                            TOrigen item = origenArray![i];
                            if (localPredicate.Ejecutar(item, dato))
                                destinoArray![destinoIndex++] = item;
                        }
                        metadatosDestino.TamañoActual = destinoIndex;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Filtra un flujo de datos representado por una estructura de ValueLINQ basándose en un predicado struct.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TDato">El tipo del dato de comparación.</typeparam>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{TOrigen, TDato}"/>.</typeparam>
        /// <param name="origen">La estructura de origen.</param>
        /// <param name="dato">El valor del dato de comparación.</param>
        /// <param name="predicado">El predicado de filtro.</param>
        /// <returns>Una estructura de ValueLINQ con los elementos filtrados.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
            this ValueLINQStruct<TOrigen> origen,
            TDato dato,
            in TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            ValueLINQStruct<TOrigen> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQStruct<TOrigen>(origenTamaño);
                if (isTokenValido)
                {
                    ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<TOrigen> metadatosDestino = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(destino.Token);
                        TOrigen[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;
                        TPredicate localPredicate = predicado;

                        for (int i = 0; i < len; i++)
                        {
                            TOrigen item = origenArray![i];
                            if (localPredicate.Ejecutar(item, dato))
                                destinoArray![destinoIndex++] = item;
                        }
                        metadatosDestino.TamañoActual = destinoIndex;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        #endregion

        #region Select

        /// <summary>
        /// Proyecta cada elemento de una estructura de referencia de ValueLINQ en un nuevo formulario utilizando un selector struct.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TPredicate">El tipo del selector que implementa <see cref="ISelectDelegado{TOrigen, TResultado}"/>.</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento de resultado.</typeparam>
        /// <param name="origen">La estructura de origen.</param>
        /// <param name="selector">El selector de proyección.</param>
        /// <returns>Una estructura de referencia de ValueLINQ con los elementos proyectados.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
            this ValueLINQRefStruct<TOrigen> origen,
            in TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            ValueLINQRefStruct<TResultado> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<TResultado>(origenTamaño);
                if (isTokenValido)
                {
                    ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<TResultado> metadatosDestino = ref ValueLINQStateManager<TResultado>.ObtenerMetadatos(destino.Token);
                        TResultado[]? destinoArray = metadatosDestino.Array;
                        TPredicate localSelector = selector;

                        for (int i = 0; i < len; i++)
                            destinoArray![i] = localSelector.Ejecutar(origenArray![i]);

                        metadatosDestino.TamañoActual = len;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Proyecta cada elemento de una estructura de ValueLINQ en un nuevo formulario utilizando un selector struct.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TPredicate">El tipo del selector que implementa <see cref="ISelectDelegado{TOrigen, TResultado}"/>.</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento de resultado.</typeparam>
        /// <param name="origen">La estructura de origen.</param>
        /// <param name="selector">El selector de proyección.</param>
        /// <returns>Una estructura de ValueLINQ con los elementos proyectados.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
            this ValueLINQStruct<TOrigen> origen,
            in TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            ValueLINQStruct<TResultado> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQStruct<TResultado>(origenTamaño);
                if (isTokenValido)
                {
                    ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<TResultado> metadatosDestino = ref ValueLINQStateManager<TResultado>.ObtenerMetadatos(destino.Token);
                        TResultado[]? destinoArray = metadatosDestino.Array;
                        TPredicate localSelector = selector;

                        for (int i = 0; i < len; i++)
                            destinoArray![i] = localSelector.Ejecutar(origenArray![i]);

                        metadatosDestino.TamañoActual = len;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        #endregion

        #region Chunk

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentOutOfRangeException(string paramName, string message)
            => throw new ArgumentOutOfRangeException(paramName, message);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArgumentNullException(string paramName)
            => throw new ArgumentNullException(paramName);

        /// <summary>
        /// Splits a <see cref="ValueLINQRefStruct{T}"/> into chunks of a specified size.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <param name="tamaño">The maximum size of each chunk.</param>
        /// <returns>A query containing the chunks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<ValueLINQStruct<T>> Chunk<T>(this ValueLINQRefStruct<T> origen, int tamaño)
        {
            if (tamaño <= 0)
                ThrowArgumentOutOfRangeException(nameof(tamaño), "El tamaño del chunk debe ser mayor que cero.");

            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            ValueLINQRefStruct<ValueLINQStruct<T>> destino = default;
            int destinoIndex = 0;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<ValueLINQStruct<T>>(cantidadChunks);
                if (isTokenValido)
                {
                    ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                    T[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destino.Token);
                        ValueLINQStruct<T>[]? destinoArray = metadatosDestino.Array;

                        for (int i = 0; i < len; i += tamaño)
                        {
                            int chunkSize = Math.Min(tamaño, len - i);
                            ValueLINQStruct<T> chunk = new(chunkSize);

                            ref MetadatosSesion<T> metadatosChunk = ref ValueLINQStateManager<T>.ObtenerMetadatos(chunk.Token);
                            origenArray!.AsSpan(i, chunkSize).CopyTo(metadatosChunk.Array!);
                            metadatosChunk.TamañoActual = chunkSize;

                            destinoArray![destinoIndex++] = chunk;
                        }
                        metadatosDestino.TamañoActual = destinoIndex;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                {
                    long destToken = destino.Token;
                    bool isDestTokenValido = destToken != 0L;
                    if (isDestTokenValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destToken);
                        bool isDestArrayValido = metadatosDestino.Array != null;
                        if (isDestArrayValido)
                            for (int i = 0; i < destinoIndex; i++)
                                metadatosDestino.Array![i].Dispose();
                        destino.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Splits a <see cref="ValueLINQStruct{T}"/> into chunks of a specified size.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <param name="tamaño">The maximum size of each chunk.</param>
        /// <returns>A query containing the chunks.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<ValueLINQStruct<T>> Chunk<T>(this ValueLINQStruct<T> origen, int tamaño)
        {
            if (tamaño <= 0)
                ThrowArgumentOutOfRangeException(nameof(tamaño), "El tamaño del chunk debe ser mayor que cero.");

            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            ValueLINQRefStruct<ValueLINQStruct<T>> destino = default;
            int destinoIndex = 0;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<ValueLINQStruct<T>>(cantidadChunks);
                if (isTokenValido)
                {
                    ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                    T[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destino.Token);
                        ValueLINQStruct<T>[]? destinoArray = metadatosDestino.Array;

                        for (int i = 0; i < len; i += tamaño)
                        {
                            int chunkSize = Math.Min(tamaño, len - i);
                            ValueLINQStruct<T> chunk = new(chunkSize);

                            ref MetadatosSesion<T> metadatosChunk = ref ValueLINQStateManager<T>.ObtenerMetadatos(chunk.Token);
                            origenArray!.AsSpan(i, chunkSize).CopyTo(metadatosChunk.Array!);
                            metadatosChunk.TamañoActual = chunkSize;

                            destinoArray![destinoIndex++] = chunk;
                        }
                        metadatosDestino.TamañoActual = destinoIndex;
                    }
                }
                isExito = true;
                return destino;
            }
            finally
            {
                origen.Dispose();
                if (!isExito)
                {
                    long destToken = destino.Token;
                    bool isDestTokenValido = destToken != 0L;
                    if (isDestTokenValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destToken);
                        bool isDestArrayValido = metadatosDestino.Array != null;
                        if (isDestArrayValido)
                            for (int i = 0; i < destinoIndex; i++)
                                metadatosDestino.Array![i].Dispose();
                        destino.Dispose();
                    }
                }
            }
        }

        #endregion

        #region ProcessChunks

        /// <summary>
        /// Procesa de forma eficiente fragmentos de elementos en una estructura de referencia de ValueLINQ utilizando un procesador struct.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos dentro del fragmento.</typeparam>
        /// <typeparam name="TProcessor">El tipo del procesador que implementa <see cref="IProcesarChunkDelegado{T}"/>.</typeparam>
        /// <param name="listaChunks">La estructura de fragmentos de origen.</param>
        /// <param name="procesarChunk">El procesador de fragmentos.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcessChunks<T, TProcessor>(
            this ValueLINQRefStruct<ValueLINQStruct<T>> listaChunks,
            TProcessor procesarChunk)
            where TProcessor : struct, IProcesarChunkDelegado<T>
        {
            long token = listaChunks.Token;
            bool isExito = false;
            try
            {
                bool isTokenValido = token != 0L;
                if (isTokenValido)
                {
                    ref MetadatosSesion<ValueLINQStruct<T>> metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                    ValueLINQStruct<T>[]? array = metadatos.Array;
                    int len = metadatos.TamañoActual;
                    bool isArrayValido = array != null && len > 0;
                    if (isArrayValido)
                        for (int i = 0; i < len; i++)
                            using (ValueLINQStruct<T> c = array![i])
                                procesarChunk.Ejecutar(c);
                }
                isExito = true;
            }
            finally
            {
                if (!isExito)
                {
                    bool isTokenValido = token != 0L;
                    if (isTokenValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                        ValueLINQStruct<T>[]? array = metadatos.Array;
                        bool isArrayValido = array != null;
                        if (isArrayValido)
                            for (int i = 0; i < metadatos.TamañoActual; i++)
                                array![i].Dispose();
                    }
                }
                listaChunks.Dispose();
            }
        }

        /// <summary>
        /// Procesa de forma eficiente fragmentos de elementos en una estructura de ValueLINQ utilizando un procesador struct.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos dentro del fragmento.</typeparam>
        /// <typeparam name="TProcessor">El tipo del procesador que implementa <see cref="IProcesarChunkDelegado{T}"/>.</typeparam>
        /// <param name="listaChunks">La estructura de fragmentos de origen.</param>
        /// <param name="procesarChunk">El procesador de fragmentos.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcessChunks<T, TProcessor>(
            this ValueLINQStruct<ValueLINQStruct<T>> listaChunks,
            TProcessor procesarChunk)
            where TProcessor : struct, IProcesarChunkDelegado<T>
        {
            long token = listaChunks.Token;
            bool isExito = false;
            try
            {
                bool isTokenValido = token != 0L;
                if (isTokenValido)
                {
                    ref MetadatosSesion<ValueLINQStruct<T>> metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                    ValueLINQStruct<T>[]? array = metadatos.Array;
                    int len = metadatos.TamañoActual;
                    bool isArrayValido = array != null && len > 0;
                    if (isArrayValido)
                        for (int i = 0; i < len; i++)
                            using (ValueLINQStruct<T> c = array![i])
                                procesarChunk.Ejecutar(c);
                }
                isExito = true;
            }
            finally
            {
                if (!isExito)
                {
                    bool isTokenValido = token != 0L;
                    if (isTokenValido)
                    {
                        ref MetadatosSesion<ValueLINQStruct<T>> metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                        ValueLINQStruct<T>[]? array = metadatos.Array;
                        bool isArrayValido = array != null;
                        if (isArrayValido)
                            for (int i = 0; i < metadatos.TamañoActual; i++)
                                array![i].Dispose();
                    }
                }
                listaChunks.Dispose();
            }
        }

        #endregion

        #region Concat

        /// <summary>
        /// Concatenates two <see cref="ValueLINQRefStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1,
            ValueLINQRefStruct<T> lista2)
        {
            ValueLINQRefStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;

                destino = new ValueLINQRefStruct<T>(len1 + len2);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                metadatosDestino.TamañoActual = len1 + len2;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates three <see cref="ValueLINQRefStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <param name="lista3">The third query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1,
            ValueLINQRefStruct<T> lista2,
            ValueLINQRefStruct<T> lista3)
        {
            ValueLINQRefStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;
                long token3 = lista3.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;

                destino = new ValueLINQRefStruct<T>(len1 + len2 + len3);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref MetadatosSesion<T> metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                metadatosDestino.TamañoActual = len1 + len2 + len3;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                lista3.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates four <see cref="ValueLINQRefStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <param name="lista3">The third query.</param>
        /// <param name="lista4">The fourth query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1,
            ValueLINQRefStruct<T> lista2,
            ValueLINQRefStruct<T> lista3,
            ValueLINQRefStruct<T> lista4)
        {
            ValueLINQRefStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0, len4 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;
                long token3 = lista3.Token;
                long token4 = lista4.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;
                if (token4 != 0L) len4 = ValueLINQStateManager<T>.ObtenerMetadatos(token4).TamañoActual;

                destino = new ValueLINQRefStruct<T>(len1 + len2 + len3 + len4);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref MetadatosSesion<T> metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                if (token4 != 0L && len4 > 0)
                {
                    ref MetadatosSesion<T> metadatos4 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token4);
                    metadatos4.Array.AsSpan(0, len4).CopyTo(destinoArray.AsSpan(offset));
                    offset += len4;
                }

                metadatosDestino.TamañoActual = len1 + len2 + len3 + len4;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                lista3.Dispose();
                lista4.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates two <see cref="ValueLINQStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1,
            ValueLINQStruct<T> lista2)
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;

                destino = new ValueLINQStruct<T>(len1 + len2);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                metadatosDestino.TamañoActual = len1 + len2;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates three <see cref="ValueLINQStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <param name="lista3">The third query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1,
            ValueLINQStruct<T> lista2,
            ValueLINQStruct<T> lista3)
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;
                long token3 = lista3.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;

                destino = new ValueLINQStruct<T>(len1 + len2 + len3);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref MetadatosSesion<T> metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                metadatosDestino.TamañoActual = len1 + len2 + len3;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                lista3.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates four <see cref="ValueLINQStruct{T}"/> instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="lista2">The second query.</param>
        /// <param name="lista3">The third query.</param>
        /// <param name="lista4">The fourth query.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1,
            ValueLINQStruct<T> lista2,
            ValueLINQStruct<T> lista3,
            ValueLINQStruct<T> lista4)
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0, len4 = 0;
                long token1 = lista1.Token;
                long token2 = lista2.Token;
                long token3 = lista3.Token;
                long token4 = lista4.Token;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;
                if (token4 != 0L) len4 = ValueLINQStateManager<T>.ObtenerMetadatos(token4).TamañoActual;

                destino = new ValueLINQStruct<T>(len1 + len2 + len3 + len4);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref MetadatosSesion<T> metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref MetadatosSesion<T> metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                if (token4 != 0L && len4 > 0)
                {
                    ref MetadatosSesion<T> metadatos4 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token4);
                    metadatos4.Array.AsSpan(0, len4).CopyTo(destinoArray.AsSpan(offset));
                    offset += len4;
                }

                metadatosDestino.TamañoActual = len1 + len2 + len3 + len4;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                lista2.Dispose();
                lista3.Dispose();
                lista4.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        /// <summary>
        /// Concatenates a <see cref="ValueLINQStruct{T}"/> with multiple other instances.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="lista1">The first query.</param>
        /// <param name="listas">The other queries to concatenate.</param>
        /// <returns>A concatenated query.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
        public static ValueLINQStruct<T> Concat<T>(this ValueLINQStruct<T> lista1, params ReadOnlySpan<ValueLINQStruct<T>> listas)
#else
        [Obsolete(JCADiagnostico.JCA0003.Mensaje, DiagnosticId = JCADiagnostico.JCA0003.Id, UrlFormat = JCADiagnostico.JCA0003.Url)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static ValueLINQStruct<T> Concat<T>(this ValueLINQStruct<T> lista1, params ValueLINQStruct<T>[] listas)
#endif
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0;
                long token1 = lista1.Token;
                if (token1 != 0L)
                    len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;

                int tamañoTotal = len1;
                foreach (ValueLINQStruct<T> lista in listas)
                {
                    long token = lista.Token;
                    if (token != 0L)
                        tamañoTotal += ValueLINQStateManager<T>.ObtenerMetadatos(token).TamañoActual;
                }

                destino = new ValueLINQStruct<T>(tamañoTotal);
                ref MetadatosSesion<T> metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;
                int currentOffset = 0;

                if (token1 != 0L && len1 > 0)
                {
                    ref MetadatosSesion<T> metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(currentOffset));
                    currentOffset += len1;
                }

                foreach (ValueLINQStruct<T> lista in listas)
                {
                    long token = lista.Token;
                    if (token != 0L)
                    {
                        ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                        int len = metadatos.TamañoActual;
                        if (len > 0)
                        {
                            metadatos.Array.AsSpan(0, len).CopyTo(destinoArray.AsSpan(currentOffset));
                            currentOffset += len;
                        }
                    }
                }

                metadatosDestino.TamañoActual = tamañoTotal;
                isExito = true;
                return destino;
            }
            finally
            {
                lista1.Dispose();
                foreach (ValueLINQStruct<T> lista in listas)
                    lista.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        #endregion

        #region Materializadores

        #region Materializadores para ValueLINQRefStruct

        /// <summary>
        /// Materializes the elements into a <see cref="PooledList{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledList{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToList<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return new PooledList<T>();
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledList<T>();

                PooledList<T> lista = new(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledListRef{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledListRef{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledListRef<T> ToListRef<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return new PooledListRef<T>();
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledListRef<T>();

                PooledListRef<T> lista = new(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledArray{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledArray<T> ToArray<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                T[] itemsVacios = ArrayPool<T>.Shared.Rent(0);
                return new PooledArray<T>(itemsVacios, 0);
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);

                PooledArray<T> array = new(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledArrayRef{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledArrayRef{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledArrayRef<T> ToArrayRef<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                T[] itemsVacios = ArrayPool<T>.Shared.Rent(0);
                return new PooledArrayRef<T>(itemsVacios, 0);
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);

                PooledArrayRef<T> array = new(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a standard array.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A standard array containing the elements.</returns>
        [Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ToArrayStandard<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return [];
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return [];

                T[] items = new T[tamaño];
                metadatos.Array.AsSpan(0, tamaño).CopyTo(items);
                return items;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a standard <see cref="List{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A standard <see cref="List{T}"/> containing the elements.</returns>
        [Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> ToListStandard<T>(this ValueLINQRefStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return [];
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return [];

                List<T> lista = new(tamaño);
                CollectionsMarshal.SetCount(lista, tamaño);
                metadatos.Array.AsSpan(0, tamaño).CopyTo(CollectionsMarshal.AsSpan(lista));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        #endregion

        #region Materializadores para ValueLINQStruct

        /// <summary>
        /// Materializes the elements into a <see cref="PooledList{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledList{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToList<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return new PooledList<T>();
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledList<T>();

                PooledList<T> lista = new(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledListRef{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledListRef{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledListRef<T> ToListRef<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return new PooledListRef<T>();
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledListRef<T>();

                PooledListRef<T> lista = new(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledArray{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledArray{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledArray<T> ToArray<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                T[] itemsVacios = ArrayPool<T>.Shared.Rent(0);
                return new PooledArray<T>(itemsVacios, 0);
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);

                PooledArray<T> array = new(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a <see cref="PooledArrayRef{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A <see cref="PooledArrayRef{T}"/> containing the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledArrayRef<T> ToArrayRef<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                T[] itemsVacios = ArrayPool<T>.Shared.Rent(0);
                return new PooledArrayRef<T>(itemsVacios, 0);
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);

                PooledArrayRef<T> array = new(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a standard array.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A standard array containing the elements.</returns>
        [Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] ToArrayStandard<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return [];
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return [];

                T[] items = new T[tamaño];
                metadatos.Array.AsSpan(0, tamaño).CopyTo(items);
                return items;
            }
            finally
            {
                origen.Dispose();
            }
        }

        /// <summary>
        /// Materializes the elements into a standard <see cref="List{T}"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="origen">The source query.</param>
        /// <returns>A standard <see cref="List{T}"/> containing the elements.</returns>
        [Obsolete(JCADiagnostico.JCA0002.Mensaje, DiagnosticId = JCADiagnostico.JCA0002.Id, UrlFormat = JCADiagnostico.JCA0002.Url)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> ToListStandard<T>(this ValueLINQStruct<T> origen)
        {
            long token = origen.Token;
            bool isTokenValido = token != 0L;

            if (!isTokenValido)
            {
                origen.Dispose();
                return [];
            }

            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return [];

                List<T> lista = new(tamaño);
                CollectionsMarshal.SetCount(lista, tamaño);
                metadatos.Array.AsSpan(0, tamaño).CopyTo(CollectionsMarshal.AsSpan(lista));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

        #endregion

        #endregion

#if NET9_0_OR_GREATER
        /// <summary>
        /// Crea una consulta de evaluación perezosa (lazy) a partir de una estructura de ValueLINQ.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos de la consulta.</typeparam>
        /// <param name="query">La consulta de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, ValueLINQSessionEnumerator}"/> configurada con un enumerador de sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>> Delay<T>(this ValueLINQStruct<T> query)
        {
            ValueLINQSessionEnumerator<T> sessionEnumerator = new(query.Token);
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator);
        }

        /// <summary>
        /// Crea una consulta de evaluación perezosa (lazy) a partir de una estructura de referencia de ValueLINQ.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos de la consulta.</typeparam>
        /// <param name="query">La consulta de origen.</param>
        /// <returns>Una estructura <see cref="ValueLINQDelayStruct{T, ValueLINQSessionEnumerator}"/> configurada con un enumerador de sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>> Delay<T>(this ValueLINQRefStruct<T> query)
        {
            ValueLINQSessionEnumerator<T> sessionEnumerator = new(query.Token);
            return new ValueLINQDelayStruct<T, ValueLINQSessionEnumerator<T>>(sessionEnumerator);
        }
#endif
    }
}
