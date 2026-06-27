using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface ISelectDelegado<TOrigen, TResultado>
    {
        TResultado Ejecutar(TOrigen objetoLista);
    }
}
