using System;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    public sealed class ValueLinqTokenInvalidoException : InvalidOperationException
    {
        public long TokenObtenido { get; }
        public int IndiceMapeado { get; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public ValueLinqTokenInvalidoException(long tokenObtenido, int indiceMapeado)
            : base($"Operación inválida en ValueLINQ: El token de sesión '{tokenObtenido}' no es válido para su procesamiento (mapea al índice [{indiceMapeado}]). Asegúrese de inicializar la estructura correctamente.")
        {
            TokenObtenido = tokenObtenido;
            IndiceMapeado = indiceMapeado;
        }
    }
}
