using System.Runtime.InteropServices;

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
