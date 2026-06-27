using System;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    public sealed class ValueLinqSesionExpiradaException : InvalidOperationException
    {
        public long IdEsperado { get; }
        public long IdObtenido { get; }

        public int Indice { get; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ValueLinqSesionExpiradaException(long idEsperado, long idObtenido, int indice) : base($"Operación inválida en ValueLINQ: La sesión del índice [{indice}] ha expirado o el buffer fue reutilizado. Se esperaba el token '{idEsperado}' pero el slot está ocupado por '{idObtenido}'.")
        {
            IdEsperado = idEsperado;
            IdObtenido = idObtenido;
            Indice = indice;
        }
    }
}
