using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Delay
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQDelayFromSpanBenchmarks
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
        public void StandardSpanIteration()
        {
            ReadOnlySpan<int> span = _array.AsSpan();
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQDelayFromSpan()
        {
#if NET9_0_OR_GREATER
            ValueLINQDelayStruct<int, ValueLINQSourceEnumerator<int>> lazyPipeline = _array.AsSpan().ToValueDelayQuery();
            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
