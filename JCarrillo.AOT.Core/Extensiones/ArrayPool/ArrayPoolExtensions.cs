using JCarrillo.AOT.Core.Colecciones.Pooled;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.Extensiones.ArrayPool
{
    public static class ArrayPoolExtensions
    {
        public static PooledList<TItem> ObtenerLista<TItem>(this ArrayPool<TItem> arrayPool, int tamaño)
        {
            TItem[] array = arrayPool.Rent(tamaño);
            return new PooledList<TItem>(array, tamaño);
        }

        public static PooledArray<TItem> ObtenerArreglo<TItem>(this ArrayPool<TItem> arrayPool, int tamaño)
        {
            TItem[] array = arrayPool.Rent(tamaño);
            return new PooledArray<TItem>(array, tamaño);
        }
    }
}
