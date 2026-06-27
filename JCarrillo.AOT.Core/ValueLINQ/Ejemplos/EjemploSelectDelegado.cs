using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ.Ejemplos
{
    internal struct EjemploSelectDelegado : ISelectDelegado<byte, int>
    {
        public int Ejectuar(byte objetoLista)
            => Convert.ToInt32(objetoLista);
    }
}
