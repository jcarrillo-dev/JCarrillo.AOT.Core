using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.Boxing;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class BoxingBenchmarks
    {
        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public object BoxingHeap()
        {
            int val = 42;
            object boxed = val;
            return boxed;
        }

        [Benchmark]
        public void ValidarNoBoxeadoStack()
        {
            int val = 42;
            val.ValidarNoBoxeado();
            _consumer.Consume(val);
        }
    }
}
