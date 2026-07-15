using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
#endif

#pragma warning disable CA1822, IDE0008, IDE0022

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.NativeAot80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [SimpleJob(RuntimeMoniker.NativeAot90)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ValueLINQDelayComparisonBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private readonly Consumer _consumer = new();

        private struct EvenFilter : IWhereDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Ejecutar(int item, int otro) => (item & 1) == 0;
        }

        private struct MultiplyByTwoSelector : ISelectDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int Ejecutar(int item) => item * 2;
        }

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            for (int i = 0; i < Size; i++)
            {
                _array[i] = i;
            }
        }

        [Benchmark(Baseline = true)]
        public void StandardLINQWhereSelect()
        {
            IEnumerable<int> query = _array.Where(static x => (x & 1) == 0).Select(static x => x * 2);
            foreach (int x in query)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQStructWhereSelect()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            ValueLINQStruct<int> filtered = query.Where(0, new EvenFilter());
            using ValueLINQStruct<int> projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelect()
        {
            ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> filtered = query.Where(0, new EvenFilter());
            using ValueLINQRefStruct<int> projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQStructWhereSelectStaticLambda()
        {
            using var projected = _array.ToValueQuery()
                                        .Where(static x => (x & 1) == 0)
                                        .Select(static x => x * 2);

            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQStructWhereSelectNoStaticLambda()
        {
            int bitMask = 1;
            int multiplier = 2;

            using var projected = _array.ToValueQuery()
                                        .Where(x => (x & bitMask) == 0)
                                        .Select(x => x * multiplier);

            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelectStaticLambda()
        {
            using var projected = _array.ToValueRefQuery()
                                        .Where(static x => (x & 1) == 0)
                                        .Select(static x => x * 2);

            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelectNoStaticLambda()
        {
            int bitMask = 1;
            int multiplier = 2;

            using var projected = _array.ToValueRefQuery()
                                        .Where(x => (x & bitMask) == 0)
                                        .Select(x => x * multiplier);

            foreach (ref int x in projected)
            {
                _consumer.Consume(x);
            }
        }

        [Benchmark]
        public void ValueLINQDelayWhereSelect()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            var lazyPipeline = _array.ToValueDelayQuery()
                                     .Where(0, ref filter)
                                     .Select<MultiplyByTwoSelector, int>(ref selector);

            foreach (ref readonly int x in lazyPipeline)
            {
                _consumer.Consume(x);
            }
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
            {
                _consumer.Consume(x);
            }
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
            {
                _consumer.Consume(x);
            }
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
