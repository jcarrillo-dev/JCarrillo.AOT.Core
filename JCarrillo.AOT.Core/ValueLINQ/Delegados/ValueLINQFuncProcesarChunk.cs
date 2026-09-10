#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System.Runtime.CompilerServices;
using static JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay.ValueLINQDelayErgonomicExtensions;

namespace JCarrillo.AOT.Core.ValueLINQ.Delegados
{
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref struct ValueLINQFuncProcesarChunk<T>(ProcesarChunkDelegado<T> delegado) : IProcesarChunkRefDelegado<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Ejecutar(ReadOnlySpan<T> listaChunk)
            => delegado(ref listaChunk);
    }
}
#endif