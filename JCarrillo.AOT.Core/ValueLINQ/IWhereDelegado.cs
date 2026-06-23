using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public interface IWhereDelegado<TOrigen, TDato>
    {
        bool Ejectuar(TOrigen objetoLista, TDato otro);
    }
}
