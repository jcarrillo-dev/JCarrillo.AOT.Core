using JCarrillo.AOT.Core.Colecciones.Pooled;
using System.Buffers;

namespace JCarrillo.AOT.Core.Extensiones.ArrayPool
{
    /// <summary>
    /// Proporciona métodos de extensión de alto rendimiento para la clase <see cref="ArrayPool{T}"/>.
    /// </summary>
    public static class ArrayPoolExtensions
    {
        /// <summary>
        /// Obtiene una lista reusable de tipo <see cref="PooledList{TItem}"/> alquilando un búfer inicial del tamaño especificado
        /// a partir del pool de arreglos.
        /// </summary>
        /// <typeparam name="TItem">El tipo de los elementos contenidos en la lista.</typeparam>
        /// <param name="arrayPool">El pool de arreglos subyacente.</param>
        /// <param name="tamaño">El tamaño inicial solicitado para la lista.</param>
        /// <returns>Una nueva instancia de <see cref="PooledList{TItem}"/> que administra el búfer alquilado.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Se lanza cuando el <paramref name="tamaño"/> es menor que cero o supera el límite del búfer devuelto.
        /// </exception>
        public static PooledList<TItem> ObtenerLista<TItem>(this ArrayPool<TItem> arrayPool, int tamaño)
        {
            TItem[] array = arrayPool.Rent(tamaño);
            return new PooledList<TItem>(array, tamaño);
        }

        /// <summary>
        /// Obtiene un arreglo reusable de tipo <see cref="PooledArray{TItem}"/> alquilando un búfer del tamaño especificado
        /// a partir del pool de arreglos.
        /// </summary>
        /// <typeparam name="TItem">El tipo de los elementos contenidos en el arreglo.</typeparam>
        /// <param name="arrayPool">El pool de arreglos subyacente.</param>
        /// <param name="tamaño">El tamaño requerido para el arreglo.</param>
        /// <returns>Una nueva instancia de <see cref="PooledArray{TItem}"/> que administra el búfer alquilado.</returns>
        /// <exception cref="System.ArgumentOutOfRangeException">
        /// Se lanza cuando el <paramref name="tamaño"/> es menor que cero o supera el límite del búfer devuelto.
        /// </exception>
        public static PooledArray<TItem> ObtenerArreglo<TItem>(this ArrayPool<TItem> arrayPool, int tamaño)
        {
            TItem[] array = arrayPool.Rent(tamaño);
            return new PooledArray<TItem>(array, tamaño);
        }
    }
}
