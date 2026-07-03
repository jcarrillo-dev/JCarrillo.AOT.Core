using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [HtmlExporter]
    public class PooledListBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private string[]? _strings;

        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            _strings = new string[Size];
            for (int i = 0; i < Size; i++)
            {
                _strings[i] = $"Item-{i}";
            }
        }

        #region Pruebas de rendimiento para Int (Tipo de valor)

        [Benchmark(Baseline = true)]
        public void ListIntDynamic()
        {
            List<int> list = [];
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Count; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void ListIntFixed()
        {
            List<int> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Count; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListIntDynamic()
        {
            using PooledList<int> list = new();
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListIntFixed()
        {
            using PooledList<int> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListRefIntDynamic()
        {
            using PooledListRef<int> list = new();
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListRefIntFixed()
        {
            using PooledListRef<int> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        #endregion

        #region Pruebas de rendimiento para String (Tipo de referencia)

        [Benchmark]
        public void ListStringDynamic()
        {
            List<string> list = [];
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Count; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void ListStringFixed()
        {
            List<string> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Count; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListStringDynamic()
        {
            using PooledList<string> list = new();
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListStringFixed()
        {
            using PooledList<string> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListRefStringDynamic()
        {
            using PooledListRef<string> list = new();
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        [Benchmark]
        public void PooledListRefStringFixed()
        {
            using PooledListRef<string> list = new(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            for (int i = 0; i < list.Tamaño; i++)
            {
                _consumer.Consume(list[i]);
            }
        }

        #endregion
    }
}
