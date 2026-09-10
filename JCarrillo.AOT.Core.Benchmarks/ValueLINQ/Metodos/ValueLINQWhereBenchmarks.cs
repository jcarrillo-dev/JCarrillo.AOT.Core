using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQWhereBenchmarks
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
        public void StandardLINQWhere()
        {
            IEnumerable<int> query = _array.Where(static x => (x & 1) == 0);
            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructWhereDelegado()
        {
            using ValueLINQStruct<int> filtered = _array.ToValueQuery().Where(0, new EvenFilter());
            foreach (ref int x in filtered)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructWhereDelegado()
        {
            using ValueLINQRefStruct<int> filtered = _array.ToValueRefQuery().Where(0, new EvenFilter());
            foreach (ref int x in filtered)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructWhereLambda()
        {
            using ValueLINQStruct<int> filtered = _array.ToValueQuery().Where(static x => (x & 1) == 0);
            foreach (ref int x in filtered)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructWhereLambda()
        {
            using ValueLINQRefStruct<int> filtered = _array.ToValueRefQuery().Where(static x => (x & 1) == 0);
            foreach (ref int x in filtered)
                _consumer.Consume(x);
        }
    }
}
