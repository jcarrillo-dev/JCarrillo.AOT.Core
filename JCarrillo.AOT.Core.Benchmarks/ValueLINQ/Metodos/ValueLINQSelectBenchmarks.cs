using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQSelectBenchmarks
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
        public void StandardLINQSelect()
        {
            IEnumerable<int> query = _array.Select(static x => x * 2);
            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructSelectDelegado()
        {
            using ValueLINQStruct<int> projected = _array.ToValueQuery().Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructSelectDelegado()
        {
            using ValueLINQRefStruct<int> projected = _array.ToValueRefQuery().Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructSelectLambda()
        {
            using ValueLINQStruct<int> projected = _array.ToValueQuery().Select(static x => x * 2);
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructSelectLambda()
        {
            using ValueLINQRefStruct<int> projected = _array.ToValueRefQuery().Select(static x => x * 2);
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }
    }
}
