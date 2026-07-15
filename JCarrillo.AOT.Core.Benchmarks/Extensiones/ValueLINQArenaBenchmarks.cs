using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.NativeAot80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [SimpleJob(RuntimeMoniker.NativeAot90)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ValueLINQArenaBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private ValueLINQArena _arenaReutilizada;

        private struct EvenFilter : IWhereDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Ejecutar(int item, int otro) => (item & 1) == 0;
        }

        private struct MultiplyByTwoSelector : ISelectDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int Ejecutar(int item) => item * 2;
        }

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            for (int i = 0; i < Size; i++)
                _array[i] = i;

            // Arena de larga vida, reutilizada por muchas consultas (la tabla se materializa una sola vez).
            _arenaReutilizada = ValueLINQArena.Crear(persistente: true);
        }

        // Coste puro de alquilar y liberar una arena (slotmap FIFO + token de arena).
        [Benchmark]
        public void CrearYDisponerArena()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
        }

        // Baseline: cadena Where+Select materializada en la arena ambiente (arena 0, enrutado implícito).
        [Benchmark(Baseline = true)]
        public int WhereSelect_Ambiente()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

        // La misma cadena en una arena explícita: mide el sobrecoste de la propagación de arena
        // por los operadores y del enrutado por id de arena, más el alquiler/liberación de la arena.
        [Benchmark]
        public int WhereSelect_ArenaExplicita()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using PooledArray<int> resultado = _array
                .ToValueQuery(arena)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

        // La misma cadena sobre una arena YA materializada y reutilizada: el coste amortizado real
        // en estado estacionario (la tabla del tipo ya existe; solo se paga el enrutado por arena).
        [Benchmark]
        public int WhereSelect_ArenaReutilizada()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery(_arenaReutilizada)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }
    }
}
