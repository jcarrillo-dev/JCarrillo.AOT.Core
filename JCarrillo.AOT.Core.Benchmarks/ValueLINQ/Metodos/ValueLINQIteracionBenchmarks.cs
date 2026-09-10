using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQIteracionBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private List<int> _list = null!;
        private ValueLINQStruct<int> _structForIteration;
        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            _list = new List<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                _array[i] = i;
                _list.Add(i);
            }

            _structForIteration = _array.ToValueQuery();
        }

        [Benchmark(Baseline = true)]
        public void ArrayIteration()
        {
            foreach (int x in _array)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIteration()
        {
            foreach (int x in _list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIterationOnly()
        {
            foreach (ref int x in _structForIteration)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIterationWithCreation()
        {
            using ValueLINQStruct<int> query = _array.ToValueQuery();
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIterationWithCreation()
        {
            using ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            foreach (ref int x in query)
                _consumer.Consume(x);
        }
    }
}
