using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class SecuenciaMaterializadaBenchmarks
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
        public void StandardLINQWhereSelectToArray()
        {
            int[] resultado = _array
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2)
                .ToArray();

            for (int i = 0; i < resultado.Length; i++)
                _consumer.Consume(resultado[i]);
        }

        [Benchmark]
        public void ValueLINQStructToArrayPooled()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            Span<int> span = resultado.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListPooled()
        {
            using PooledList<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToList();

            Span<int> span = resultado.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToArrayStandardHeap()
        {
            int[] resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArrayStandard();

            for (int i = 0; i < resultado.Length; i++)
                _consumer.Consume(resultado[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListStandardHeap()
        {
            List<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToListStandard();

            for (int i = 0; i < resultado.Count; i++)
                _consumer.Consume(resultado[i]);
        }

        [Benchmark]
        public void ValueLINQRefStructToArrayPooled()
        {
            using PooledArray<int> resultado = _array
                .ToValueRefQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            Span<int> span = resultado.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQRefStructToListPooled()
        {
            using PooledList<int> resultado = _array
                .ToValueRefQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToList();

            Span<int> span = resultado.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }
    }
}
