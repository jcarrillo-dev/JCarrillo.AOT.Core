using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados
{
    /// <summary>
    /// Delegado struct para multiplicar por dos sin asignaciones ni boxing.
    /// </summary>
    public readonly struct MultiplyByTwoSelector : ISelectDelegado<int, int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Ejecutar(int item) => item * 2;
    }
}
