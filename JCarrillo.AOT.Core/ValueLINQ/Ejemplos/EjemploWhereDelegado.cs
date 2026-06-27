using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ.Ejemplos
{
    internal struct EjemploWhereDelegado<T> : IWhereDelegado<T, T>
    {
        public bool Ejectuar(T objetoLista, T otro)
        {
            bool 
                objetoListaEsNulo = objetoLista is null,
                otroEsNulo = otro is null;

            if (objetoListaEsNulo && otroEsNulo)
                return true;

            if (objetoListaEsNulo || otroEsNulo) 
                return false;

            return objetoLista!.Equals(otro);
        }
    }
}
