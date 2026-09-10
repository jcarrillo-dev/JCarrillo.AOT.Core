using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones.Pooled
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class PooledListStringBenchmarks
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
        public void ListStringDynamic()
        {
            List<string> list = [];
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void ListStringFixed()
        {
            List<string> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListStringDynamic()
        {
            using PooledList<string> list = new();
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListStringFixed()
        {
            using PooledList<string> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListRefStringDynamic()
        {
            using PooledListRef<string> list = new();
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void PooledListRefStringFixed()
        {
            using PooledListRef<string> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(_strings[i]);
            for (int i = 0; i < list.Tamaño; i++)
                _consumer.Consume(list[i]);
        }
    }
}
