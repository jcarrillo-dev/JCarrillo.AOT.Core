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
        public int BoxingInterfaceHeap()
        {
            int val = 42;
#pragma warning disable CA1859 // Use concrete types when possible for performance - intentional boxing benchmark
            IComparable comp = val;
#pragma warning restore CA1859
            return comp.CompareTo(42);
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
