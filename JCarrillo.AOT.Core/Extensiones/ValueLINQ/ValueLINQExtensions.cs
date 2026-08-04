using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
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
        /// Copia el contenido del origen en la sesión identificada por el token, liberando la sesión si la copia falla.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token de la sesión de destino recién creada.</param>
        /// <param name="origen">Los elementos de origen a copiar.</param>
        /// <remarks>
        /// Método frío compartido por todas las sobrecargas de construcción (array/Span/ReadOnlySpan ×
        /// ValueLINQStruct/ValueLINQRefStruct). Concentra el bloque try/catch para que los wrappers públicos
        /// queden sin manejo de excepciones y sean inlineables en net8/net9. La ventana entre el constructor de
        /// la consulta y la entrada al try ya existía en el código original; este método conserva la garantía de
        /// liberación vía catch. Liberar por token es exactamente lo que hace Dispose() en ambas estructuras
        /// (un reenvío a <see cref="ValueLINQStateManager{T}.LiberarMetadatos(long)"/>).
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CopiarOrigenSlow<T>(long token, scoped ReadOnlySpan<T> origen)
        {
            try
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                origen.CopyTo(metadatos.Array);
                metadatos.TamañoActual = origen.Length;
            }
            catch
            {
                ValueLINQStateManager<T>.LiberarMetadatos(token);
                throw;
            }
        }

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
            CopiarOrigenSlow<T>(query.Token, origen.AsSpan(0, origen.Length));
            return query;
        }

        /// <summary>
        /// Convierte un array en un <see cref="ValueLINQStruct{T}"/> cuya sesión vive en la arena indicada.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos del array.</typeparam>
        /// <param name="origen">El array de origen.</param>
        /// <param name="arena">La arena propietaria de la consulta y de sus sesiones intermedias.</param>
        /// <returns>Un <see cref="ValueLINQStruct{T}"/> con los elementos del array, adscrito a la arena.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this T[] origen, ValueLINQArena arena)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQStruct<T> query = new(arena.Id, origen.Length);
            CopiarOrigenSlow<T>(query.Token, origen.AsSpan(0, origen.Length));
            return query;
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
            CopiarOrigenSlow<T>(query.Token, origen.AsSpan(0, origen.Length));
            return query;
        }

        /// <summary>
        /// Convierte un array en un <see cref="ValueLINQRefStruct{T}"/> cuya sesión vive en la arena indicada.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos del array.</typeparam>
        /// <param name="origen">El array de origen.</param>
        /// <param name="arena">La arena propietaria de la consulta y de sus sesiones intermedias.</param>
        /// <returns>Un <see cref="ValueLINQRefStruct{T}"/> con los elementos del array, adscrito a la arena.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this T[] origen, ValueLINQArena arena)
        {
            if (origen == null)
                ThrowArgumentNullException(nameof(origen));

            ValueLINQRefStruct<T> query = new(arena.Id, origen.Length);
            CopiarOrigenSlow<T>(query.Token, origen.AsSpan(0, origen.Length));
            return query;
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
            CopiarOrigenSlow<T>(query.Token, origen);
            return query;
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
            CopiarOrigenSlow<T>(query.Token, origen);
            return query;
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
            CopiarOrigenSlow<T>(query.Token, origen);
            return query;
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
            CopiarOrigenSlow<T>(query.Token, origen);
            return query;
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
            scoped in TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            // La lectura de metadatos permanece en el wrapper, fuera de todo EH: si lanza, origen no se
            // libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            return WhereSlow(origen, origenTamaño, dato, in predicado);
        }

        /// <summary>
        /// Camino frío de Where para <see cref="ValueLINQRefStruct{T}"/>: concentra el try/finally para que el
        /// wrapper público quede sin manejo de excepciones y sea inlineable en net8/net9.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TDato">El tipo del dato de comparación.</typeparam>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{TOrigen, TDato}"/>.</typeparam>
        /// <param name="origen">La estructura de origen (por valor; su Dispose libera por token).</param>
        /// <param name="origenTamaño">El tamaño del origen, calculado en el wrapper fuera del EH.</param>
        /// <param name="dato">El valor del dato de comparación.</param>
        /// <param name="predicado">El predicado de filtro.</param>
        /// <returns>Una estructura de referencia de ValueLINQ con los elementos filtrados.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQRefStruct<TOrigen> WhereSlow<TOrigen, TDato, TPredicate>(
            ValueLINQRefStruct<TOrigen> origen,
            int origenTamaño,
            TDato dato,
            scoped in TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            ValueLINQRefStruct<TOrigen> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<TOrigen>(TokenHelper.ObtenerArenaId(origenToken), origenTamaño);
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
            // La lectura de metadatos permanece en el wrapper, fuera de todo EH: si lanza, origen no se
            // libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            return WhereSlow<TOrigen, TDato, TPredicate>(origenToken, origenTamaño, dato, in predicado);
        }

        /// <summary>
        /// Camino frío de Where para <see cref="ValueLINQStruct{T}"/>: concentra el try/finally para que el
        /// wrapper público quede sin manejo de excepciones y sea inlineable en net8/net9.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TDato">El tipo del dato de comparación.</typeparam>
        /// <typeparam name="TPredicate">El tipo del predicado que implementa <see cref="IWhereDelegado{TOrigen, TDato}"/>.</typeparam>
        /// <param name="origenToken">El token del origen; liberar por token equivale exactamente a origen.Dispose().</param>
        /// <param name="origenTamaño">El tamaño del origen, calculado en el wrapper fuera del EH.</param>
        /// <param name="dato">El valor del dato de comparación.</param>
        /// <param name="predicado">El predicado de filtro.</param>
        /// <returns>Una estructura de ValueLINQ con los elementos filtrados.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQStruct<TOrigen> WhereSlow<TOrigen, TDato, TPredicate>(
            long origenToken,
            int origenTamaño,
            TDato dato,
            in TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            bool isTokenValido = origenToken != 0L;
            ValueLINQStruct<TOrigen> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQStruct<TOrigen>(TokenHelper.ObtenerArenaId(origenToken), origenTamaño);
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
                // LiberarMetadatos(origenToken) es exactamente el cuerpo de origen.Dispose().
                ValueLINQStateManager<TOrigen>.LiberarMetadatos(origenToken);
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
            scoped in TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            // La lectura de metadatos permanece en el wrapper, fuera de todo EH: si lanza, origen no se
            // libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            return SelectSlow<TOrigen, TPredicate, TResultado>(origenToken, origenTamaño, isTokenValido, in selector);
        }

        /// <summary>
        /// Camino frío de Select para <see cref="ValueLINQRefStruct{T}"/>: concentra el try/finally para que el
        /// wrapper público quede sin manejo de excepciones y sea inlineable en net8/net9.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TPredicate">El tipo del selector que implementa <see cref="ISelectDelegado{TOrigen, TResultado}"/>.</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento de resultado.</typeparam>
        /// <param name="origenToken">El token del origen; liberar por token equivale exactamente a origen.Dispose().</param>
        /// <param name="origenTamaño">El tamaño del origen, calculado en el wrapper fuera del EH.</param>
        /// <param name="isTokenValido">Indica si el token del origen es válido (distinto de 0).</param>
        /// <param name="selector">El selector de proyección; 'scoped' garantiza que la referencia no escapa en el valor devuelto.</param>
        /// <returns>Una estructura de referencia de ValueLINQ con los elementos proyectados.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQRefStruct<TResultado> SelectSlow<TOrigen, TPredicate, TResultado>(
            long origenToken,
            int origenTamaño,
            bool isTokenValido,
            scoped in TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            ValueLINQRefStruct<TResultado> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<TResultado>(TokenHelper.ObtenerArenaId(origenToken), origenTamaño);
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
                // LiberarMetadatos(origenToken) es exactamente el cuerpo de origen.Dispose().
                ValueLINQStateManager<TOrigen>.LiberarMetadatos(origenToken);
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
            // La lectura de metadatos permanece en el wrapper, fuera de todo EH: si lanza, origen no se
            // libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<TOrigen> metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            return SelectSlow<TOrigen, TPredicate, TResultado>(origen, in selector, origenTamaño);
        }

        /// <summary>
        /// Camino frío de Select para <see cref="ValueLINQStruct{T}"/>: concentra el try/finally para que el
        /// wrapper público quede sin manejo de excepciones y sea inlineable en net8/net9.
        /// </summary>
        /// <typeparam name="TOrigen">El tipo de los elementos de origen.</typeparam>
        /// <typeparam name="TPredicate">El tipo del selector que implementa <see cref="ISelectDelegado{TOrigen, TResultado}"/>.</typeparam>
        /// <typeparam name="TResultado">El tipo del elemento de resultado.</typeparam>
        /// <param name="origen">La estructura de origen (por valor, 8 bytes readonly; su Dispose libera por token).</param>
        /// <param name="selector">El selector de proyección; nunca escapa (solo se copia a una local).</param>
        /// <param name="origenTamaño">El tamaño del origen, calculado en el wrapper fuera del EH.</param>
        /// <returns>Una estructura de ValueLINQ con los elementos proyectados.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQStruct<TResultado> SelectSlow<TOrigen, TPredicate, TResultado>(
            ValueLINQStruct<TOrigen> origen,
            scoped in TPredicate selector,
            int origenTamaño)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            ValueLINQStruct<TResultado> destino = default;
            bool isExito = false;
            try
            {
                destino = new ValueLINQStruct<TResultado>(TokenHelper.ObtenerArenaId(origenToken), origenTamaño);
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

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArenaCruzada(int arenaEsperada, int arenaEncontrada)
            => throw new ValueLinqArenaCruzadaException(arenaEsperada, arenaEncontrada);

        // Verifica que un operando pertenezca a la arena de destino (la del primer operando).
        // Un token 0 (consulta default/vacía) no impone arena y se omite.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ValidarMismaArena(int arenaDestino, long token)
        {
            if (token == 0L)
                return;

            int arena = TokenHelper.ObtenerArenaId(token);
            if (arena != arenaDestino)
                ThrowArenaCruzada(arenaDestino, arena);
        }

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

            // La validación y la lectura de metadatos permanecen en el wrapper, fuera de todo EH:
            // si lanzan, origen no se libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            return ChunkSlow<T>(origenToken, cantidadChunks, tamaño);
        }

        /// <summary>
        /// Camino frío de Chunk compartido por ambas sobrecargas (ValueLINQRefStruct y ValueLINQStruct):
        /// concentra el try/finally para que los wrappers públicos queden sin manejo de excepciones y sean
        /// inlineables en net8/net9. Solo cruzan tipos escalares (long/int), ningún ref struct.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="origenToken">El token del origen; liberar por token equivale exactamente a origen.Dispose() en ambas sobrecargas.</param>
        /// <param name="cantidadChunks">El número de chunks a crear, calculado en el wrapper.</param>
        /// <param name="tamaño">El tamaño máximo de cada chunk (ya validado en el wrapper).</param>
        /// <returns>Una consulta que contiene los chunks.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQRefStruct<ValueLINQStruct<T>> ChunkSlow<T>(long origenToken, int cantidadChunks, int tamaño)
        {
            bool isTokenValido = origenToken != 0L;
            ValueLINQRefStruct<ValueLINQStruct<T>> destino = default;
            int destinoIndex = 0;
            bool isExito = false;
            try
            {
                destino = new ValueLINQRefStruct<ValueLINQStruct<T>>(TokenHelper.ObtenerArenaId(origenToken), cantidadChunks);
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
                            ValueLINQStruct<T> chunk = new(TokenHelper.ObtenerArenaId(origenToken), chunkSize);

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
                // LiberarMetadatos(origenToken) es exactamente el cuerpo de origen.Dispose() en ambas sobrecargas.
                ValueLINQStateManager<T>.LiberarMetadatos(origenToken);
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

            // La validación y la lectura de metadatos permanecen en el wrapper, fuera de todo EH:
            // si lanzan, origen no se libera, igual que en el código original.
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref MetadatosSesion<T> metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            return ChunkSlow<T>(origenToken, cantidadChunks, tamaño);
        }

        #endregion

        #region ProcesarChunks

        /// <summary>
        /// Procesa de forma eficiente fragmentos de elementos en una estructura de referencia de ValueLINQ utilizando un procesador struct.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos dentro del fragmento.</typeparam>
        /// <typeparam name="TProcessor">El tipo del procesador que implementa <see cref="IProcesarChunkDelegado{T}"/>.</typeparam>
        /// <param name="listaChunks">La estructura de fragmentos de origen.</param>
        /// <param name="procesarChunk">El procesador de fragmentos.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcesarChunks<T, TProcessor>(
            this ValueLINQRefStruct<ValueLINQStruct<T>> listaChunks,
            TProcessor procesarChunk)
            where TProcessor : struct, IProcesarChunkDelegado<T>
            => ProcesarChunksSlow<T, TProcessor>(listaChunks.Token, procesarChunk);

        /// <summary>
        /// Camino frío de ProcesarChunks compartido por ambas sobrecargas (ValueLINQRefStruct y ValueLINQStruct):
        /// concentra el try/finally para que los wrappers públicos queden sin manejo de excepciones y sean
        /// inlineables en net8/net9. Solo cruzan el token (long) y el procesador struct, ningún ref struct.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos dentro del fragmento.</typeparam>
        /// <typeparam name="TProcessor">El tipo del procesador que implementa <see cref="IProcesarChunkDelegado{T}"/>.</typeparam>
        /// <param name="token">El token de la lista de chunks; liberar por token equivale exactamente a listaChunks.Dispose() en ambas sobrecargas.</param>
        /// <param name="procesarChunk">El procesador de fragmentos.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ProcesarChunksSlow<T, TProcessor>(long token, TProcessor procesarChunk)
            where TProcessor : struct, IProcesarChunkDelegado<T>
        {
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
                // LiberarMetadatos(token) es exactamente el cuerpo de listaChunks.Dispose() en ambas sobrecargas.
                // Ver el invariante del #region "Caminos fríos compartidos": Dispose es hoy un mero reenvío a LiberarMetadatos(Token).
                ValueLINQStateManager<ValueLINQStruct<T>>.LiberarMetadatos(token);
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
        public static void ProcesarChunks<T, TProcessor>(
            this ValueLINQStruct<ValueLINQStruct<T>> listaChunks,
            TProcessor procesarChunk)
            where TProcessor : struct, IProcesarChunkDelegado<T>
            => ProcesarChunksSlow<T, TProcessor>(listaChunks.Token, procesarChunk);

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
            => ConcatRefSlow<T>(lista1.Token, lista2.Token, 0L, 0L);

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
            => ConcatRefSlow<T>(lista1.Token, lista2.Token, lista3.Token, 0L);

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
            => ConcatRefSlow<T>(lista1.Token, lista2.Token, lista3.Token, lista4.Token);

        /// <summary>
        /// Camino frío de Concat compartido por las sobrecargas de aridad 2, 3 y 4 sobre
        /// <see cref="ValueLINQRefStruct{T}"/>: concentra el try/finally para que los wrappers públicos queden
        /// sin manejo de excepciones y sean inlineables en net8/net9. A diferencia de Where/Select/Chunk, las
        /// lecturas de metadatos y <see cref="ValidarMismaArena"/> permanecen DENTRO del try porque en el Concat
        /// original todo fallo libera las fuentes.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token1">El token de la primera lista; liberar por token equivale exactamente a lista1.Dispose().</param>
        /// <param name="token2">El token de la segunda lista; liberar por token equivale exactamente a lista2.Dispose().</param>
        /// <param name="token3">El token de la tercera lista, o 0L si el slot está ausente (aridad menor); 0L es no-op garantizado en lectura, validación, copia y liberación.</param>
        /// <param name="token4">El token de la cuarta lista, o 0L si el slot está ausente (aridad menor); 0L es no-op garantizado en lectura, validación, copia y liberación.</param>
        /// <returns>Una consulta concatenada.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQRefStruct<T> ConcatRefSlow<T>(long token1, long token2, long token3, long token4)
        {
            ValueLINQRefStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0, len4 = 0;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;
                if (token4 != 0L) len4 = ValueLINQStateManager<T>.ObtenerMetadatos(token4).TamañoActual;

                int arenaDestino = TokenHelper.ObtenerArenaId(token1);
                ValidarMismaArena(arenaDestino, token2);
                ValidarMismaArena(arenaDestino, token3);
                ValidarMismaArena(arenaDestino, token4);
                destino = new ValueLINQRefStruct<T>(arenaDestino, len1 + len2 + len3 + len4);
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
                // LiberarMetadatos(tokenN) es exactamente el cuerpo de listaN.Dispose(); con token 0 es no-op
                // estricto (TablaSesiones.LiberarMetadatos hace early-return). No eliminar los guards token != 0L
                // creyéndolos redundantes: sostienen las aridades 2 y 3, que pasan 0L en los slots finales.
                ValueLINQStateManager<T>.LiberarMetadatos(token1);
                ValueLINQStateManager<T>.LiberarMetadatos(token2);
                ValueLINQStateManager<T>.LiberarMetadatos(token3);
                ValueLINQStateManager<T>.LiberarMetadatos(token4);
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
            => ConcatSlow<T>(lista1.Token, lista2.Token, 0L, 0L);

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
            => ConcatSlow<T>(lista1.Token, lista2.Token, lista3.Token, 0L);

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
            => ConcatSlow<T>(lista1.Token, lista2.Token, lista3.Token, lista4.Token);

        /// <summary>
        /// Camino frío de Concat compartido por las sobrecargas de aridad 2, 3 y 4 sobre
        /// <see cref="ValueLINQStruct{T}"/>: concentra el try/finally para que los wrappers públicos queden sin
        /// manejo de excepciones y sean inlineables en net8/net9. A diferencia de Where/Select/Chunk, las
        /// lecturas de metadatos y <see cref="ValidarMismaArena"/> permanecen DENTRO del try porque en el Concat
        /// original todo fallo libera las fuentes.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token1">El token de la primera lista; liberar por token equivale exactamente a lista1.Dispose().</param>
        /// <param name="token2">El token de la segunda lista; liberar por token equivale exactamente a lista2.Dispose().</param>
        /// <param name="token3">El token de la tercera lista, o 0L si el slot está ausente (aridad menor); 0L es no-op garantizado en lectura, validación, copia y liberación.</param>
        /// <param name="token4">El token de la cuarta lista, o 0L si el slot está ausente (aridad menor); 0L es no-op garantizado en lectura, validación, copia y liberación.</param>
        /// <returns>Una consulta concatenada.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQStruct<T> ConcatSlow<T>(long token1, long token2, long token3, long token4)
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0, len2 = 0, len3 = 0, len4 = 0;

                if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
                if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
                if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;
                if (token4 != 0L) len4 = ValueLINQStateManager<T>.ObtenerMetadatos(token4).TamañoActual;

                int arenaDestino = TokenHelper.ObtenerArenaId(token1);
                ValidarMismaArena(arenaDestino, token2);
                ValidarMismaArena(arenaDestino, token3);
                ValidarMismaArena(arenaDestino, token4);
                destino = new ValueLINQStruct<T>(arenaDestino, len1 + len2 + len3 + len4);
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
                // LiberarMetadatos(tokenN) es exactamente el cuerpo de listaN.Dispose(); con token 0 es no-op
                // estricto (TablaSesiones.LiberarMetadatos hace early-return). No eliminar los guards token != 0L
                // creyéndolos redundantes: sostienen las aridades 2 y 3, que pasan 0L en los slots finales.
                ValueLINQStateManager<T>.LiberarMetadatos(token1);
                ValueLINQStateManager<T>.LiberarMetadatos(token2);
                ValueLINQStateManager<T>.LiberarMetadatos(token3);
                ValueLINQStateManager<T>.LiberarMetadatos(token4);
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
            => ConcatParamsSlow<T>(lista1.Token, listas);

        /// <summary>
        /// Camino frío de la sobrecarga variádica de Concat sobre <see cref="ValueLINQStruct{T}"/>: concentra el
        /// try/finally para que los wrappers públicos queden sin manejo de excepciones y sean inlineables en
        /// net8/net9. A diferencia de Where/Select/Chunk, las lecturas de metadatos y
        /// <see cref="ValidarMismaArena"/> permanecen DENTRO del try porque en el Concat original todo fallo
        /// libera las fuentes.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token1">El token de la primera lista; liberar por token equivale exactamente a lista1.Dispose() (con 0L es no-op).</param>
        /// <param name="listas">Las demás listas a concatenar. En net8 el wrapper recibe un array por params: si se
        /// invoca con un array null explícito, el span queda vacío (antes se producía NRE dentro del try; ahora el
        /// resultado es una copia de lista1 con las fuentes ya liberadas).</param>
        /// <returns>Una consulta concatenada.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ValueLINQStruct<T> ConcatParamsSlow<T>(long token1, scoped ReadOnlySpan<ValueLINQStruct<T>> listas)
        {
            ValueLINQStruct<T> destino = default;
            bool isExito = false;
            try
            {
                int len1 = 0;
                if (token1 != 0L)
                    len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;

                int arenaDestino = TokenHelper.ObtenerArenaId(token1);
                int tamañoTotal = len1;
                foreach (ValueLINQStruct<T> lista in listas)
                {
                    long token = lista.Token;
                    if (token != 0L)
                    {
                        ValidarMismaArena(arenaDestino, token);
                        tamañoTotal += ValueLINQStateManager<T>.ObtenerMetadatos(token).TamañoActual;
                    }
                }

                destino = new ValueLINQStruct<T>(arenaDestino, tamañoTotal);
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
                // LiberarMetadatos(token1) es exactamente el cuerpo de lista1.Dispose() (con 0L es no-op estricto).
                ValueLINQStateManager<T>.LiberarMetadatos(token1);
                foreach (ValueLINQStruct<T> lista in listas)
                    lista.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        #endregion

        #region Materializadores

        #region Caminos fríos compartidos

        // INVARIANTE: estos métodos fríos liberan por token en el finally porque Dispose() de
        // ValueLINQStruct<T> y de ValueLINQRefStruct<T> es hoy un mero reenvío a
        // ValueLINQStateManager<T>.LiberarMetadatos(Token). Si algún día Dispose ganara estado por
        // instancia, estos helpers deberían actualizarse en consecuencia.

        /// <summary>
        /// Camino frío de ToList compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Un <see cref="PooledList{T}"/> con los elementos.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PooledList<T> ToListSlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        /// <summary>
        /// Camino frío de ToListRef compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Un <see cref="PooledListRef{T}"/> con los elementos (envuelve un array del heap; es legal devolverlo).</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PooledListRef<T> ToListRefSlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        /// <summary>
        /// Camino frío de ToArray compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Un <see cref="PooledArray{T}"/> con los elementos.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PooledArray<T> ToArraySlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        /// <summary>
        /// Camino frío de ToArrayRef compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Un <see cref="PooledArrayRef{T}"/> con los elementos (envuelve un array del heap; es legal devolverlo).</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static PooledArrayRef<T> ToArrayRefSlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        /// <summary>
        /// Camino frío de ToArrayStandard compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Un arreglo estándar del heap con los elementos.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T[] ToArrayStandardSlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        /// <summary>
        /// Camino frío de ToListStandard compartido por ambas sobrecargas: concentra el try/finally para que los
        /// wrappers públicos queden sin manejo de excepciones y sean inlineables en net8/net9.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos.</typeparam>
        /// <param name="token">El token válido (distinto de 0) de la sesión de origen.</param>
        /// <returns>Una <see cref="List{T}"/> estándar con los elementos.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<T> ToListStandardSlow<T>(long token)
        {
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
                ValueLINQStateManager<T>.LiberarMetadatos(token);
            }
        }

        #endregion

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

            return ToListSlow<T>(token);
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

            return ToListRefSlow<T>(token);
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

            return ToArraySlow<T>(token);
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

            return ToArrayRefSlow<T>(token);
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

            return ToArrayStandardSlow<T>(token);
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

            return ToListStandardSlow<T>(token);
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

            return ToListSlow<T>(token);
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

            return ToListRefSlow<T>(token);
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

            return ToArraySlow<T>(token);
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

            return ToArrayRefSlow<T>(token);
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

            return ToArrayStandardSlow<T>(token);
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

            return ToListStandardSlow<T>(token);
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
