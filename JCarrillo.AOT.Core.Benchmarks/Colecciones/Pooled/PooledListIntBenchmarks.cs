using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class PooledListIntBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public void ListIntDynamic()
        {
            List<int> list = [];
            for (int i = 0; i < Size; i++)
                list.Add(i);
            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void ListIntFixed()
        {
            List<int> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(i);
            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListIntDynamic()
        {
            using PooledList<int> list = new();
            for (int i = 0; i < Size; i++)
                list.Add(i);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListIntFixed()
        {
            using PooledList<int> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(i);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }
    }
}
