namespace JCarrillo.AOT.Core.ValueLINQ
{
    internal struct MetadatosSesion<T>
    {
        public long Token;
        public T[]? Array;
        public int TamañoActual;
        public bool IsDisposed;
        public long UltimoAcceso;
        public long Version;
        public long Relleno1;
        public long Relleno2;
        public long Relleno3;
    }
}
