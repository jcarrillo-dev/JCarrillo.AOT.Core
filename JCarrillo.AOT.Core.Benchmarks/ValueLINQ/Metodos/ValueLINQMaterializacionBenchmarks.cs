using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQMaterializacionBenchmarks
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
        public void StandardLINQToArray()
        {
            int[] array = _array.Where(static x => (x & 1) == 0).ToArray();
            for (int i = 0; i < array.Length; i++)
                _consumer.Consume(array[i]);
        }

        [Benchmark]
        public void ValueLINQStructToArrayPooled()
        {
            using PooledArray<int> array = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .ToArray();

            Span<int> span = array.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToArrayStandardHeap()
        {
            int[] array = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .ToArrayStandard();

            for (int i = 0; i < array.Length; i++)
                _consumer.Consume(array[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListPooled()
        {
            using PooledList<int> list = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .ToList();

            Span<int> span = list.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListStandardHeap()
        {
            List<int> list = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .ToListStandard();

            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        [Benchmark]
        public void ValueLINQRefStructToArrayPooled()
        {
            using PooledArray<int> array = _array
                .ToValueRefQuery()
                .Where(0, new EvenFilter())
                .ToArray();

            Span<int> span = array.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQRefStructToListPooled()
        {
            using PooledList<int> list = _array
                .ToValueRefQuery()
                .Where(0, new EvenFilter())
                .ToList();

            Span<int> span = list.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }
    }
}
