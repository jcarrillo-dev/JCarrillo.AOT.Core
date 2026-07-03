using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Ejemplos
{
    internal struct EjemploSelectDelegado : ISelectDelegado<byte, int>
    {
        public readonly int Ejecutar(byte objetoLista)
            => Convert.ToInt32(objetoLista);
    }
}
