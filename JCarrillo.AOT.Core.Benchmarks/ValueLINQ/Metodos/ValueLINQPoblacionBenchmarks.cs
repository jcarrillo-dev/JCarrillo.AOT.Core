using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQPoblacionBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            for (int i = 0; i < Size; i++)
                _array[i] = i;
        }

        [Benchmark(Baseline = true)]
        public void ListIntDynamic()
        {
            List<int> list = [];
            for (int i = 0; i < Size; i++)
                list.Add(i);
            foreach (int x in list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIntFixed()
        {
            List<int> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(i);
            foreach (int x in list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIntBlock()
        {
            List<int> list = new(Size);
#if NET9_0_OR_GREATER
            list.AddRange(_array.AsSpan());
#else
            System.Runtime.InteropServices.CollectionsMarshal.SetCount(list, Size);
            _array.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
#endif
            foreach (int x in list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntDynamic()
        {
            using ValueLINQStruct<int> query = new(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntFixed()
        {
            using ValueLINQStruct<int> query = new(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntBlock()
        {
            using ValueLINQStruct<int> query = new(Size);
            query.Añadir(_array.AsSpan());
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntDynamic()
        {
            using ValueLINQRefStruct<int> query = new(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntFixed()
        {
            using ValueLINQRefStruct<int> query = new(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntBlock()
        {
            using ValueLINQRefStruct<int> query = new(Size);
            query.Añadir(_array.AsSpan());
            foreach (ref int x in query)
                _consumer.Consume(x);
        }
    }
}
