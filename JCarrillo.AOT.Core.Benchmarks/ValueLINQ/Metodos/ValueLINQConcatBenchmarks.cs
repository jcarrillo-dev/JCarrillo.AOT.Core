using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQConcatBenchmarks
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
        public void StandardLINQConcat4Elements()
        {
            IEnumerable<int> query = _array.Concat(_array).Concat(_array).Concat(_array);
            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructConcatStatic4Elements()
        {
            ValueLINQStruct<int> q1 = _array.ToValueQuery();
            ValueLINQStruct<int> q2 = _array.ToValueQuery();
            ValueLINQStruct<int> q3 = _array.ToValueQuery();
            ValueLINQStruct<int> q4 = _array.ToValueQuery();

            using ValueLINQStruct<int> concatenated = q1.Concat(q2, q3, q4);
            foreach (ref int x in concatenated)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructConcatParams5Elements()
        {
            ValueLINQStruct<int> q1 = _array.ToValueQuery();
            ValueLINQStruct<int> q2 = _array.ToValueQuery();
            ValueLINQStruct<int> q3 = _array.ToValueQuery();
            ValueLINQStruct<int> q4 = _array.ToValueQuery();
            ValueLINQStruct<int> q5 = _array.ToValueQuery();

            using ValueLINQStruct<int> concatenated = q1.Concat(q2, q3, q4, q5);
            foreach (ref int x in concatenated)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructConcatStatic4Elements()
        {
            ValueLINQRefStruct<int> q1 = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> q2 = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> q3 = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> q4 = _array.ToValueRefQuery();

            using ValueLINQRefStruct<int> concatenated = q1.Concat(q2, q3, q4);
            foreach (ref int x in concatenated)
                _consumer.Consume(x);
        }
    }
}
