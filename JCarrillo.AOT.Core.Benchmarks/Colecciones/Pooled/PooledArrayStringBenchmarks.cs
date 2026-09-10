using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class PooledArrayStringBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private string[] _strings = null!;
        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            _strings = new string[Size];
            for (int i = 0; i < Size; i++)
                _strings[i] = $"Item-{i}";
        }

        [Benchmark(Baseline = true)]
        public void StandardArray()
        {
            string[] arr = new string[Size];
            for (int i = 0; i < Size; i++)
                arr[i] = _strings[i];
            for (int i = 0; i < Size; i++)
                _consumer.Consume(arr[i]);
        }

        [Benchmark]
        public void PooledArray()
        {
            using PooledArray<string> arr = new(Size);
            for (int i = 0; i < Size; i++)
                arr[i] = _strings[i];
            for (int i = 0; i < Size; i++)
                _consumer.Consume(arr[i]);
        }
    }
}
