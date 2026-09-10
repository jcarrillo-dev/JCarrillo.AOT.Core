using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados
{
    /// <summary>
    /// Delegado struct para evaluar números pares sin asignaciones ni boxing.
    /// </summary>
    public readonly struct EvenFilter : IWhereDelegado<int, int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Ejecutar(int item, int otro) => (item & 1) == 0;
    }
}
