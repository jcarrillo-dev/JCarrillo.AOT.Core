using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled.Ref
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class PooledArrayRefBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public void StandardArray()
        {
            int[] arr = new int[Size];
            for (int i = 0; i < Size; i++)
                arr[i] = i;
            for (int i = 0; i < Size; i++)
                _consumer.Consume(arr[i]);
        }

        [Benchmark]
        public void PooledArrayRef()
        {
            using PooledArrayRef<int> arr = new(Size);
            for (int i = 0; i < Size; i++)
                arr[i] = i;
            for (int i = 0; i < Size; i++)
                _consumer.Consume(arr[i]);
        }
    }
}
