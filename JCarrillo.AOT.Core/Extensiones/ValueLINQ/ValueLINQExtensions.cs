using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ
{
    public static partial class ValueLINQExtensions
    {
        #region ToValueQuery

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this T[] origen)
        {
            if (origen == null)
                throw new ArgumentNullException(nameof(origen));

            var query = new ValueLINQStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this T[] origen)
        {
            if (origen == null)
                throw new ArgumentNullException(nameof(origen));

            var query = new ValueLINQRefStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this Span<T> origen)
        {
            var query = new ValueLINQStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this Span<T> origen)
        {
            var query = new ValueLINQRefStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ReadOnlySpan<T> origen)
        {
            var query = new ValueLINQStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ReadOnlySpan<T> origen)
        {
            var query = new ValueLINQRefStruct<T>(origen.Length);
            try
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(query.Token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ref Memory<T> origen)
            => origen.Span.ToValueQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref Memory<T> origen)
            => origen.Span.ToValueRefQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> ToValueQuery<T>(this ref PooledList<T> origen)
            => origen.Span.ToValueQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> ToValueRefQuery<T>(this ref PooledList<T> origen)
            => origen.Span.ToValueRefQuery();

        #endregion

        #region Where

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
            this ValueLINQRefStruct<TOrigen> origen, 
            TDato dato, 
            TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            var destino = new ValueLINQRefStruct<TOrigen>(origenTamaño);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(destino.Token);
                        TOrigen[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;

                        for (int i = 0; i < len; i++)
                        {
                            TOrigen item = origenArray![i];
                            if (predicado.Ejecutar(item, dato))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
            this ValueLINQStruct<TOrigen> origen, 
            TDato dato, 
            TPredicate predicado)
            where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            var destino = new ValueLINQStruct<TOrigen>(origenTamaño);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(destino.Token);
                        TOrigen[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;

                        for (int i = 0; i < len; i++)
                        {
                            TOrigen item = origenArray![i];
                            if (predicado.Ejecutar(item, dato))
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
            this ValueLINQRefStruct<TOrigen> origen, 
            TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            var destino = new ValueLINQRefStruct<TResultado>(origenTamaño);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<TResultado>.ObtenerMetadatos(destino.Token);
                        TResultado[]? destinoArray = metadatosDestino.Array;

                        for (int i = 0; i < len; i++)
                            destinoArray![i] = selector.Ejecutar(origenArray![i]);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
            this ValueLINQStruct<TOrigen> origen, 
            TPredicate selector)
            where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
        {
            int origenTamaño = 0;
            long origenToken = origen.Token;
            bool isTokenValido = origenToken != 0L;
            if (isTokenValido)
            {
                ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            var destino = new ValueLINQStruct<TResultado>(origenTamaño);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<TOrigen>.ObtenerMetadatos(origenToken);
                    TOrigen[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<TResultado>.ObtenerMetadatos(destino.Token);
                        TResultado[]? destinoArray = metadatosDestino.Array;

                        for (int i = 0; i < len; i++)
                            destinoArray![i] = selector.Ejecutar(origenArray![i]);

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
        private static void ThrowArgumentOutOfRangeException(string paramName, string message)
            => throw new ArgumentOutOfRangeException(paramName, message);

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
                ref var metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            var destino = new ValueLINQRefStruct<ValueLINQStruct<T>>(cantidadChunks);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                    T[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destino.Token);
                        ValueLINQStruct<T>[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;

                        for (int i = 0; i < len; i += tamaño)
                        {
                            int chunkSize = Math.Min(tamaño, len - i);
                            var chunk = new ValueLINQStruct<T>(chunkSize);
                            
                            ref var metadatosChunk = ref ValueLINQStateManager<T>.ObtenerMetadatos(chunk.Token);
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
                        ref var metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destToken);
                        bool isDestArrayValido = metadatosDestino.Array != null;
                        if (isDestArrayValido)
                            for (int i = 0; i < metadatosDestino.TamañoActual; i++)
                                metadatosDestino.Array![i].Dispose();
                        destino.Dispose();
                    }
                }
            }
        }

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
                ref var metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                origenTamaño = metadatosOrigen.TamañoActual;
            }

            int cantidadChunks = (origenTamaño + tamaño - 1) / tamaño;
            var destino = new ValueLINQRefStruct<ValueLINQStruct<T>>(cantidadChunks);
            bool isExito = false;
            try
            {
                if (isTokenValido)
                {
                    ref var metadatosOrigen = ref ValueLINQStateManager<T>.ObtenerMetadatos(origenToken);
                    T[]? origenArray = metadatosOrigen.Array;
                    int len = metadatosOrigen.TamañoActual;
                    bool isArrayValido = origenArray != null && len > 0;
                    if (isArrayValido)
                    {
                        ref var metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destino.Token);
                        ValueLINQStruct<T>[]? destinoArray = metadatosDestino.Array;
                        int destinoIndex = 0;

                        for (int i = 0; i < len; i += tamaño)
                        {
                            int chunkSize = Math.Min(tamaño, len - i);
                            var chunk = new ValueLINQStruct<T>(chunkSize);
                            
                            ref var metadatosChunk = ref ValueLINQStateManager<T>.ObtenerMetadatos(chunk.Token);
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
                        ref var metadatosDestino = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(destToken);
                        bool isDestArrayValido = metadatosDestino.Array != null;
                        if (isDestArrayValido)
                            for (int i = 0; i < metadatosDestino.TamañoActual; i++)
                                metadatosDestino.Array![i].Dispose();
                        destino.Dispose();
                    }
                }
            }
        }

        #endregion

        #region ProcessChunks

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
                    ref var metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                    ValueLINQStruct<T>[]? array = metadatos.Array;
                    int len = metadatos.TamañoActual;
                    bool isArrayValido = array != null && len > 0;
                    if (isArrayValido)
                        for (int i = 0; i < len; i++)
                            using (var c = array![i])
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
                        ref var metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
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
                    ref var metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
                    ValueLINQStruct<T>[]? array = metadatos.Array;
                    int len = metadatos.TamañoActual;
                    bool isArrayValido = array != null && len > 0;
                    if (isArrayValido)
                        for (int i = 0; i < len; i++)
                            using (var c = array![i])
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
                        ref var metadatos = ref ValueLINQStateManager<ValueLINQStruct<T>>.ObtenerMetadatos(token);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1, 
            ValueLINQRefStruct<T> lista2)
        {
            int len1 = 0, len2 = 0;
            long token1 = lista1.Token;
            long token2 = lista2.Token;

            if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
            if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;

            var destino = new ValueLINQRefStruct<T>(len1 + len2);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1, 
            ValueLINQRefStruct<T> lista2, 
            ValueLINQRefStruct<T> lista3)
        {
            int len1 = 0, len2 = 0, len3 = 0;
            long token1 = lista1.Token;
            long token2 = lista2.Token;
            long token3 = lista3.Token;

            if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
            if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
            if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;

            var destino = new ValueLINQRefStruct<T>(len1 + len2 + len3);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref var metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQRefStruct<T> Concat<T>(
            this ValueLINQRefStruct<T> lista1, 
            ValueLINQRefStruct<T> lista2, 
            ValueLINQRefStruct<T> lista3, 
            ValueLINQRefStruct<T> lista4)
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

            var destino = new ValueLINQRefStruct<T>(len1 + len2 + len3 + len4);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref var metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                if (token4 != 0L && len4 > 0)
                {
                    ref var metadatos4 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token4);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1, 
            ValueLINQStruct<T> lista2)
        {
            int len1 = 0, len2 = 0;
            long token1 = lista1.Token;
            long token2 = lista2.Token;

            if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
            if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;

            var destino = new ValueLINQStruct<T>(len1 + len2);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1, 
            ValueLINQStruct<T> lista2, 
            ValueLINQStruct<T> lista3)
        {
            int len1 = 0, len2 = 0, len3 = 0;
            long token1 = lista1.Token;
            long token2 = lista2.Token;
            long token3 = lista3.Token;

            if (token1 != 0L) len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;
            if (token2 != 0L) len2 = ValueLINQStateManager<T>.ObtenerMetadatos(token2).TamañoActual;
            if (token3 != 0L) len3 = ValueLINQStateManager<T>.ObtenerMetadatos(token3).TamañoActual;

            var destino = new ValueLINQStruct<T>(len1 + len2 + len3);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref var metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQStruct<T> Concat<T>(
            this ValueLINQStruct<T> lista1, 
            ValueLINQStruct<T> lista2, 
            ValueLINQStruct<T> lista3, 
            ValueLINQStruct<T> lista4)
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

            var destino = new ValueLINQStruct<T>(len1 + len2 + len3 + len4);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;

                int offset = 0;
                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(offset));
                    offset += len1;
                }

                if (token2 != 0L && len2 > 0)
                {
                    ref var metadatos2 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token2);
                    metadatos2.Array.AsSpan(0, len2).CopyTo(destinoArray.AsSpan(offset));
                    offset += len2;
                }

                if (token3 != 0L && len3 > 0)
                {
                    ref var metadatos3 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token3);
                    metadatos3.Array.AsSpan(0, len3).CopyTo(destinoArray.AsSpan(offset));
                    offset += len3;
                }

                if (token4 != 0L && len4 > 0)
                {
                    ref var metadatos4 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token4);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
        public static ValueLINQStruct<T> Concat<T>(this ValueLINQStruct<T> lista1, params ReadOnlySpan<ValueLINQStruct<T>> listas)
#else
        [Obsolete("En .NET 8.0 este método genera un array intermedio (Heap Allocation). Se recomienda actualizar a .NET 9+ o evitar 'params'.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static ValueLINQStruct<T> Concat<T>(this ValueLINQStruct<T> lista1, params ValueLINQStruct<T>[] listas)
#endif
        {
            int len1 = 0;
            long token1 = lista1.Token;
            if (token1 != 0L)
                len1 = ValueLINQStateManager<T>.ObtenerMetadatos(token1).TamañoActual;

            int tamañoTotal = len1;
            foreach (var lista in listas)
            {
                long token = lista.Token;
                if (token != 0L)
                    tamañoTotal += ValueLINQStateManager<T>.ObtenerMetadatos(token).TamañoActual;
            }

            var destino = new ValueLINQStruct<T>(tamañoTotal);
            bool isExito = false;
            try
            {
                ref var metadatosDestino = ref ValueLINQStateManager<T>.ObtenerMetadatos(destino.Token);
                T[]? destinoArray = metadatosDestino.Array;
                int currentOffset = 0;

                if (token1 != 0L && len1 > 0)
                {
                    ref var metadatos1 = ref ValueLINQStateManager<T>.ObtenerMetadatos(token1);
                    metadatos1.Array.AsSpan(0, len1).CopyTo(destinoArray.AsSpan(currentOffset));
                    currentOffset += len1;
                }

                foreach (var lista in listas)
                {
                    long token = lista.Token;
                    if (token != 0L)
                    {
                        ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
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
                foreach (var lista in listas)
                    lista.Dispose();
                if (!isExito)
                    destino.Dispose();
            }
        }

        #endregion

        #region Materializadores

        #region Materializadores para ValueLINQRefStruct

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledList<T>();

                var lista = new PooledList<T>(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledListRef<T>();

                var lista = new PooledListRef<T>(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);
                var array = new PooledArray<T>(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);
                var array = new PooledArrayRef<T>(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        #endregion

        #region Materializadores para ValueLINQStruct

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledList<T>();

                var lista = new PooledList<T>(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;

                if (tamaño == 0)
                    return new PooledListRef<T>();

                var lista = new PooledListRef<T>(tamaño);
                lista.AddRange(metadatos.Array.AsSpan(0, tamaño));
                return lista;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);
                var array = new PooledArray<T>(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

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
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                int tamaño = metadatos.TamañoActual;
                T[] items = ArrayPool<T>.Shared.Rent(tamaño);
                var array = new PooledArrayRef<T>(items, tamaño);

                if (tamaño > 0)
                    metadatos.Array.AsSpan(0, tamaño).CopyTo(array.Span);

                return array;
            }
            finally
            {
                origen.Dispose();
            }
        }

        #endregion

        #endregion
    }
}
