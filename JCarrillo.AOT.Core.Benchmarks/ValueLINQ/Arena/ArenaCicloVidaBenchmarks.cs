using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Arena
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArenaCicloVidaBenchmarks
    {
        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public void AsignacionGCHeap()
        {
            List<int> list = new(64);
            _consumer.Consume(list);
        }

        [Benchmark]
        public void CrearYDisponerArenaTransitoria()
            => ValueLINQArena.Crear().Dispose();

        [Benchmark]
        public void CrearYDisponerArenaPersistente()
            => ValueLINQArena.Crear(persistente: true).Dispose();
    }
}
