using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Ejemplos
{
    internal struct EjemploWhereDelegado<T> : IWhereDelegado<T, T>
    {
        public readonly bool Ejecutar(T objetoLista, T otro)
        {
            bool
                objetoListaEsNulo = objetoLista is null,
                otroEsNulo = otro is null;

            return objetoListaEsNulo
                ? otroEsNulo
                : !otroEsNulo && objetoLista!.Equals(otro);
        }
    }
}
