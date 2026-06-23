using JCarrillo.AOT.Core.Colecciones.Pooled;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public interface IProcesarChunkDelegado<T>
    {
        void Ejecutar(PooledList<T> listaChunk);
    }
}
