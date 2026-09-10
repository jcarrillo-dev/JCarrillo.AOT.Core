using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Arena
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArenaConsultasBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private ValueLINQArena _arenaReutilizada;

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            for (int i = 0; i < Size; i++)
                _array[i] = i;

            _arenaReutilizada = ValueLINQArena.Crear(persistente: true);
        }

        [Benchmark(Baseline = true)]
        public int StandardLINQWhereSelectToArray()
        {
            int[] resultado = _array
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2)
                .ToArray();

            return resultado.Length;
        }

        [Benchmark]
        public int WhereSelect_Ambiente()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

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

        [Benchmark]
        public int DelayWhereSelect_Ambiente()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            using PooledArray<int> resultado = _array
                .ToValueDelayQuery()
                .Where(0, ref filter)
                .Select<MultiplyByTwoSelector, int>(ref selector)
                .ToArray();

            return resultado.Tamaño;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public int DelayWhereSelect_ArenaReutilizada()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            using PooledArray<int> resultado = _array
                .ToValueDelayQuery(_arenaReutilizada)
                .Where(0, ref filter)
                .Select<MultiplyByTwoSelector, int>(ref selector)
                .ToArray();

            return resultado.Tamaño;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public int DelayChunk_Ambiente()
        {
#if NET9_0_OR_GREATER
            int elementos = 0;

            foreach (ReadOnlySpan<int> fragmento in _array.ToValueDelayQuery().Chunk(64))
                elementos += fragmento.Length;

            return elementos;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public int DelayChunk_ArenaReutilizada()
        {
#if NET9_0_OR_GREATER
            int elementos = 0;

            foreach (ReadOnlySpan<int> fragmento in _array.ToValueDelayQuery(_arenaReutilizada).Chunk(64))
                elementos += fragmento.Length;

            return elementos;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
