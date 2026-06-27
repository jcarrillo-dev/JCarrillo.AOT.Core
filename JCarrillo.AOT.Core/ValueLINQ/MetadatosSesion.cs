using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    [StructLayout(LayoutKind.Auto)]
    internal struct MetadatosSesion<T>
    {
        public long Token;
        public T[]? Array;
        public int TamañoActual;
        public bool IsDisposed;
        public long UltimoAcceso;
    }
}
