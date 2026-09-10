using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Arena
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArenaChunkBenchmarks
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

        [GlobalCleanup]
        public void Cleanup()
            => _arenaReutilizada.Dispose();

        [Benchmark(Baseline = true)]
        public int StandardLINQChunk()
        {
            int elementos = 0;
            foreach (int[] fragmento in _array.Chunk(64))
                elementos += fragmento.Length;

            return elementos;
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
