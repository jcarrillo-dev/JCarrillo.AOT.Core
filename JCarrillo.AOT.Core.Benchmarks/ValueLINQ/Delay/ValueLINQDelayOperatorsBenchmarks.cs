using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Delay
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQDelayOperatorsBenchmarks
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
        public void ValueLINQDelayWhereSelectDelegados()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            var lazyPipeline = _array.ToValueDelayQuery()
                .Where(0, ref filter)
                .Select<MultiplyByTwoSelector, int>(ref selector);

            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public void ValueLINQDelayWhereSelectStaticLambda()
        {
#if NET9_0_OR_GREATER
            var lazyPipeline = _array.ToValueDelayQuery()
                .Where(static x => (x & 1) == 0)
                .Select(static x => x * 2);

            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        [Benchmark]
        public void ValueLINQDelayWhereSelectNoStaticLambda()
        {
#if NET9_0_OR_GREATER
            int bitMask = 1;
            int multiplier = 2;

            var lazyPipeline = _array.ToValueDelayQuery()
                .Where(x => (x & bitMask) == 0)
                .Select(x => x * multiplier);

            foreach (ref readonly int x in lazyPipeline)
                _consumer.Consume(x);
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
