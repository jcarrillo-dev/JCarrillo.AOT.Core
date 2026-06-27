using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
    public class ValueLINQBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private List<int> _list = null!;
        private ValueLINQStruct<int> _structForIteration;

        // Struct delegates for ValueLINQ
        private struct EvenFilter : IWhereDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Ejecutar(int item, int otro) => (item & 1) == 0;
        }

        private struct MultiplyByTwoSelector : ISelectDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Ejecutar(int item) => item * 2;
        }

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            _list = new List<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                _array[i] = i;
                _list.Add(i);
            }

            // Pre-populated struct for pure iteration benchmark
            _structForIteration = _array.ToValueQuery();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // No-op. We avoid calling Dispose here because of a bug in the production ValueLINQStateManager
            // background cleanup which causes IndexOutOfRangeException when disposing pre-allocated resources.
            // Since each benchmark runs in a separate process, leaking this resource is completely harmless.
        }

        #region Population Benchmarks

        [Benchmark(Baseline = true)]
        public int List_Int_Dynamic()
        {
            var list = new List<int>();
            for (int i = 0; i < Size; i++)
                list.Add(i);
            return list.Count;
        }

        [Benchmark]
        public int List_Int_Fixed()
        {
            var list = new List<int>(Size);
            for (int i = 0; i < Size; i++)
                list.Add(i);
            return list.Count;
        }

        [Benchmark]
        public int ValueLINQStruct_Int_Dynamic()
        {
            using var query = new ValueLINQStruct<int>(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            return Size;
        }

        [Benchmark]
        public int ValueLINQStruct_Int_Fixed()
        {
            using var query = new ValueLINQStruct<int>(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            return Size;
        }

        [Benchmark]
        public int ValueLINQRefStruct_Int_Dynamic()
        {
            using var query = new ValueLINQRefStruct<int>(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            return Size;
        }

        [Benchmark]
        public int ValueLINQRefStruct_Int_Fixed()
        {
            using var query = new ValueLINQRefStruct<int>(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            return Size;
        }

        [Benchmark]
        public int ValueLINQStruct_Int_Block()
        {
            using var query = new ValueLINQStruct<int>(Size);
            query.Añadir(_array.AsSpan());
            return Size;
        }

        [Benchmark]
        public int ValueLINQRefStruct_Int_Block()
        {
            using var query = new ValueLINQRefStruct<int>(Size);
            query.Añadir(_array.AsSpan());
            return Size;
        }

        [Benchmark]
        public int List_Int_Block()
        {
            var list = new List<int>(Size);
#if NET9_0_OR_GREATER
            list.AddRange(_array.AsSpan());
#else
            System.Runtime.InteropServices.CollectionsMarshal.SetCount(list, Size);
            _array.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
#endif
            return list.Count;
        }

        #endregion

        #region Iteration Benchmarks

        [Benchmark]
        public int Array_Iteration()
        {
            int sum = 0;
            foreach (int x in _array)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int List_Iteration()
        {
            int sum = 0;
            foreach (int x in _list)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQStruct_Iteration_Only()
        {
            int sum = 0;
            foreach (ref int x in _structForIteration)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQStruct_Iteration_WithCreation()
        {
            using var query = _array.ToValueQuery();
            int sum = 0;
            foreach (ref int x in query)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQRefStruct_Iteration_WithCreation()
        {
            using var query = _array.ToValueRefQuery();
            int sum = 0;
            foreach (ref int x in query)
                sum += x;
            return sum;
        }

        #endregion

        #region Fluent Operator Benchmarks (Where & Select)

        [Benchmark]
        public int StandardLINQ_Where_Select()
        {
            int sum = 0;
            var query = _array.Where(x => x % 2 == 0).Select(x => x * 2);
            foreach (int x in query)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQStruct_Where_Select()
        {
            var query = _array.ToValueQuery();
            var filtered = query.Where(0, new EvenFilter());
            using var projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            int sum = 0;
            foreach (ref int x in projected)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQRefStruct_Where_Select()
        {
            var query = _array.ToValueRefQuery();
            var filtered = query.Where(0, new EvenFilter());
            using var projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            int sum = 0;
            foreach (ref int x in projected)
                sum += x;
            return sum;
        }

        #endregion

        #region Concat Allocation Benchmarks (Static vs Params)

        [Benchmark]
        public int ValueLINQStruct_Concat_Static_4Elements()
        {
            var q1 = _array.ToValueQuery();
            var q2 = _array.ToValueQuery();
            var q3 = _array.ToValueQuery();
            var q4 = _array.ToValueQuery();

            using var concatenated = q1.Concat(q2, q3, q4);
            
            int sum = 0;
            foreach (ref int x in concatenated)
                sum += x;
            return sum;
        }

        [Benchmark]
        public int ValueLINQStruct_Concat_Params_5Elements()
        {
            var q1 = _array.ToValueQuery();
            var q2 = _array.ToValueQuery();
            var q3 = _array.ToValueQuery();
            var q4 = _array.ToValueQuery();
            var q5 = _array.ToValueQuery();

#pragma warning disable CS0618
            using var concatenated = q1.Concat(q2, q3, q4, q5);
#pragma warning restore CS0618

            int sum = 0;
            foreach (ref int x in concatenated)
                sum += x;
            return sum;
        }

        #endregion
    }
}
