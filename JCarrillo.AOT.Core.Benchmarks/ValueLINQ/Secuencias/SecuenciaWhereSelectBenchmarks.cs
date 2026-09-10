using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class SecuenciaWhereSelectBenchmarks
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
        public void StandardLINQWhereSelect()
        {
            IEnumerable<int> query = _array.Where(static x => (x & 1) == 0).Select(static x => x * 2);
            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructWhereSelectDelegados()
        {
            using ValueLINQStruct<int> projected = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());

            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelectDelegados()
        {
            using ValueLINQRefStruct<int> projected = _array
                .ToValueRefQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());

            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructWhereSelectLambdas()
        {
            using ValueLINQStruct<int> projected = _array
                .ToValueQuery()
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelectLambdas()
        {
            using ValueLINQRefStruct<int> projected = _array
                .ToValueRefQuery()
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (ref int x in projected)
                _consumer.Consume(x);
        }
    }
}
