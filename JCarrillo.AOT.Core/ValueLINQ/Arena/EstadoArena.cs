namespace JCarrillo.AOT.Core.ValueLINQ.Arena
{
    internal struct EstadoArena
    {
        public long Token;
        public long Generacion;
        public long UltimoUso;
        public long InactividadTicks;
        public bool IsPersistente;
    }
}
