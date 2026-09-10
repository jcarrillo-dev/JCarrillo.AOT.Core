using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Delay
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQDelayFromArrayBenchmarks
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
        public void StandardEnumerableFromArray()
        {
            foreach (int x in _array.AsEnumerable())
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQDelayFromArray()
        {
#if NET9_0_OR_GREATER
            ValueLINQDelayStruct<int, ValueLINQSourceEnumerator<int>> lazyPipeline = _array.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public void ValueLINQDelayFromEagerStructQuery()
        {
#if NET9_0_OR_GREATER
            using ValueLINQStruct<int> query = _array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> lazyPipeline = query.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public void ValueLINQDelayFromEagerRefStructQuery()
        {
#if NET9_0_OR_GREATER
            using ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> lazyPipeline = query.ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
