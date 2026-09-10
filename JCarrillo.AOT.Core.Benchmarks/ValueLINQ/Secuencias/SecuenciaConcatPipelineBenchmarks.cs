using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class SecuenciaConcatPipelineBenchmarks
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
        public void StandardLINQConcatPipeline()
        {
            IEnumerable<int> query = _array
                .Concat(_array)
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructConcatPipeline()
        {
            ValueLINQStruct<int> q1 = _array.ToValueQuery();
            ValueLINQStruct<int> q2 = _array.ToValueQuery();

            using ValueLINQStruct<int> pipeline = q1
                .Concat(q2)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());

            foreach (ref int x in pipeline)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructConcatPipeline()
        {
            ValueLINQRefStruct<int> q1 = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> q2 = _array.ToValueRefQuery();

            using ValueLINQRefStruct<int> pipeline = q1
                .Concat(q2)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());

            foreach (ref int x in pipeline)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructConcatPipelineLambdas()
        {
            ValueLINQStruct<int> q1 = _array.ToValueQuery();
            ValueLINQStruct<int> q2 = _array.ToValueQuery();

            using ValueLINQStruct<int> pipeline = q1
                .Concat(q2)
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (ref int x in pipeline)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructConcatPipelineLambdas()
        {
            ValueLINQRefStruct<int> q1 = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> q2 = _array.ToValueRefQuery();

            using ValueLINQRefStruct<int> pipeline = q1
                .Concat(q2)
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (ref int x in pipeline)
                _consumer.Consume(x);
        }
    }
}
