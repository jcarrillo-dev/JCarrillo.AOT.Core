#if NET9_0_OR_GREATER
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [SimpleJob(RuntimeMoniker.Net90)]
    [SimpleJob(RuntimeMoniker.NativeAot90)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ValueLINQDelayBenchmarks
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
            {
                _array[i] = i;
            }
        }

        [Benchmark]
        public void ValueLINQDelayFromArray()
        {
            ValueLINQDelayStruct<int, ValueLINQSourceEnumerator<int>> lazyPipeline = _array.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQDelayFromSpan()
        {
            ValueLINQDelayStruct<int, ValueLINQSourceEnumerator<int>> lazyPipeline = _array.AsSpan().ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQDelayFromEagerStructQuery()
        {
            using ValueLINQStruct<int> query = _array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> lazyPipeline = query.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQDelayFromEagerRefStructQuery()
        {
            using ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> lazyPipeline = query.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
            {
                _consumer.Consume(x);
            }
        }
    }
}
#endif
