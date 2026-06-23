using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.ValueLINQ;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ
{
    public static partial class ValueLINQExtensions
    {
        #region ToValueQuery

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToValueQuery<T>(this T[] origen)
            => origen.AsSpan().ToValueQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToValueQuery<T>(this ref Memory<T> origen)
            => origen.Span.ToValueQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToValueQuery(this ref PooledList<T> origen)
            => origen.Span.ToValueQuery();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<T> ToValueQuery<T>(this Span<T> origen)
        {
            PooledList<T> lista = new(origen.Length);
            lista.AddRange(origen);
            return lista;
        }

        #endregion

        #region Where

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<TOrigen> Where<TOrigen, TDato, TDelegado>(this ref PooledList<TOrigen> origen, TDato dato, TDelegado predicado)
            where TDelegado : struct, IWhereDelegado<TOrigen, TDato>
        {
            PooledList<TOrigen> listaFiltrada = new(origen.Tamaño);

            foreach (TOrigen item in origen.Span)
            {
                if (predicado.Ejectuar(item, dato))
                    listaFiltrada.Add(item);
            }

            origen.Dispose();

            return listaFiltrada;
        }

        #endregion

        #region Select

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static PooledList<TResultado> Select<TOrigen, TDelegado, TResultado>(this ref PooledList<TOrigen> origen, TDelegado selector)
            where TDelegado : struct, ISelectDelegado<TOrigen, TResultado>
        {
            PooledList<TResultado> listaTransformada = new(origen.Tamaño);

            foreach (TOrigen item in origen.Span)
            {
                TResultado resultado = selector.Ejectuar(item);
                listaTransformada.Add(resultado);
            }

            origen.Dispose();

            return listaTransformada;
        }

        #endregion

        #region Chunck

        [DoesNotReturn]
        private static void ThrowArgumentOutOfRangeException(string paramName, string message)
            => throw new ArgumentOutOfRangeException(paramName, message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<PooledList<T>> Chunk<T>(this ref PooledList<T> origen, int tamaño)
        {
            if (tamaño <= 0)
                ThrowArgumentOutOfRangeException(nameof(tamaño), "El tamaño del chunk debe ser mayor que cero.");

            PooledList<PooledList<T>> listaChunks = new PooledList<PooledList<T>>();
            for (int i = 0; i < origen.Tamaño; i += tamaño)
            {
                int chunkSize = Math.Min(tamaño, origen.Tamaño - i);
                PooledList<T> chunk = new PooledList<T>(chunkSize);
                for (int j = 0; j < chunkSize; j++)
                {
                    chunk.Add(origen.Span[i + j]);
                }
                listaChunks.Add(chunk);
            }

            origen.Dispose();

            return listaChunks;
        }

        #endregion

        #region ProcessChunks

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProcessChunks<T, TDelegado>(this ref PooledList<PooledList<T>> listaChunks, TDelegado procesarChunk)
            where TDelegado : struct, IProcesarChunkDelegado<T>
        {
            foreach (PooledList<T> chunk in listaChunks.Span)
            {
                using PooledList<T> c = chunk;
                procesarChunk.Ejecutar(c);
            }

            listaChunks.Dispose();
        }

        #endregion

        #region Concat

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
        public static PooledList<T> Concat<T>(this ref PooledList<T> lista1, params ReadOnlySpan<PooledList<T>> listas)
#else
        [Obsolete("En .NET 8.0 este método genera un array intermedio (Heap Allocation). Se recomienda actualizar a .NET 9+ o evitar 'params'.", false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static PooledList<T> Concat<T>(this ref PooledList<T> lista1, params PooledList<T>[] listas)
#endif
        {
            int tamañoTotal = lista1.Tamaño;

            foreach (PooledList<T> lista in listas)
                tamañoTotal += lista.Tamaño;

            PooledList<T> listaResultado = new(tamañoTotal);

            listaResultado.AddRange(lista1.Span);
            lista1.Dispose();

            foreach (PooledList<T> lista in listas)
            {
                listaResultado.AddRange(lista.Span);
                lista.Dispose();
            }

            return listaResultado;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<TOrigen> Concat<TOrigen>(this ref PooledList<TOrigen> lista1, PooledList<TOrigen> lista2)
        {
            int tamañoTotal = lista1.Tamaño + lista2.Tamaño;

            PooledList<TOrigen> listaResultado = new(tamañoTotal);

            listaResultado.AddRange(lista1.Span);
            listaResultado.AddRange(lista2.Span);

            lista1.Dispose();
            lista2.Dispose();

            return listaResultado;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<TOrigen> Concat<TOrigen>(this ref PooledList<TOrigen> lista1, PooledList<TOrigen> lista2, PooledList<TOrigen> lista3)
        {
            int tamañoTotal = lista1.Tamaño + lista2.Tamaño + lista3.Tamaño;

            PooledList<TOrigen> listaResultado = new(tamañoTotal);

            listaResultado.AddRange(lista1.Span);
            listaResultado.AddRange(lista2.Span);
            listaResultado.AddRange(lista3.Span);

            lista1.Dispose();
            lista2.Dispose();
            lista3.Dispose();

            return listaResultado;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<TOrigen> Concat<TOrigen>(this ref PooledList<TOrigen> lista1, PooledList<TOrigen> lista2, PooledList<TOrigen> lista3, PooledList<TOrigen> lista4)
        {
            int tamañoTotal = lista1.Tamaño + lista2.Tamaño + lista3.Tamaño + lista4.Tamaño;

            PooledList<TOrigen> listaResultado = new(tamañoTotal);

            listaResultado.AddRange(lista1.Span);
            listaResultado.AddRange(lista2.Span);
            listaResultado.AddRange(lista3.Span);
            listaResultado.AddRange(lista4.Span);

            lista1.Dispose();
            lista2.Dispose();
            lista3.Dispose();
            lista4.Dispose();

            return listaResultado;
        }

        #endregion
    }
}
