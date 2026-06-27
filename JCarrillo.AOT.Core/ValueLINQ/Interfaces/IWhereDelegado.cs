using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IWhereDelegado<TOrigen, TDato>
    {
        bool Ejecutar(TOrigen objetoLista, TDato otro);
    }
}
