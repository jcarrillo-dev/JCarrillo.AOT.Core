#if NET9_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;
using JCarrillo.AOT.Core.ValueLINQ.Delay;

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay
{
    /// <summary>
    /// Métodos de extensión para materializar consultas de evaluación perezosa.
    /// </summary>
    internal static class ValueLINQDelayMaterializerExtensions
    {
        /// <summary>
        /// Materializa el flujo de datos perezoso en una lista agrupada de memoria eficiente (<see cref="PooledList{T}"/>).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso a materializar.</param>
        /// <returns>Una instancia de <see cref="PooledList{T}"/> que contiene los elementos del flujo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PooledList<T> ToList<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            TEnumerator enumerator = pipeline.GetEnumerator();
            try
            {
                PooledList<T> lista = new();
                while (enumerator.MoveNext())
                {
                    lista.Add(enumerator.Current);
                }
                return lista;
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        /// <summary>
        /// Materializa el flujo de datos perezoso en un arreglo agrupado de memoria eficiente (<see cref="PooledArray{T}"/>).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso a materializar.</param>
        /// <returns>Una instancia de <see cref="PooledArray{T}"/> que contiene los elementos del flujo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static PooledArray<T> ToArray<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            TEnumerator enumerator = pipeline.GetEnumerator();
            try
            {
                using PooledList<T> lista = new();
                while (enumerator.MoveNext())
                {
                    lista.Add(enumerator.Current);
                }
                PooledArray<T> arreglo = ArrayPool<T>.Shared.ObtenerArreglo(lista.Tamaño);
                lista.Span.CopyTo(arreglo.Span);
                return arreglo;
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        /// <summary>
        /// Materializa el flujo de datos perezoso en una lista estándar de .NET (<see cref="List{T}"/>).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso a materializar.</param>
        /// <returns>Una lista estándar <see cref="List{T}"/> que contiene los elementos del flujo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static List<T> ToListStandard<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            TEnumerator enumerator = pipeline.GetEnumerator();
            try
            {
                using PooledList<T> temp = new();
                while (enumerator.MoveNext())
                {
                    temp.Add(enumerator.Current);
                }

                if (temp.Tamaño == 0)
                    return [];

                List<T> lista = new(temp.Tamaño);
                CollectionsMarshal.SetCount(lista, temp.Tamaño);
                temp.Span.CopyTo(CollectionsMarshal.AsSpan(lista));
                return lista;
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        /// <summary>
        /// Materializa el flujo de datos perezoso en un arreglo estándar de .NET (<typeparamref name="T"/>[]).
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos en el flujo de datos. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador del flujo de origen. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="pipeline">El flujo de datos perezoso a materializar.</param>
        /// <returns>Un arreglo estándar de tipo <typeparamref name="T"/>[] que contiene los elementos del flujo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T[] ToArrayStandard<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
        {
            TEnumerator enumerator = pipeline.GetEnumerator();
            try
            {
                using PooledList<T> lista = new();
                while (enumerator.MoveNext())
                {
                    lista.Add(enumerator.Current);
                }
                return lista.Span.ToArray();
            }
            finally
            {
                enumerator.Dispose();
            }
        }
    }
}
#endif
