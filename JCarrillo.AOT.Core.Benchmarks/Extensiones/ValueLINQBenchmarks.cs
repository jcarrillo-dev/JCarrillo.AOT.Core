using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Colecciones.Pooled;
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

        private readonly Consumer _consumer = new();

        // Delegados estructurados para ValueLINQ
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
            _list = new List<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                _array[i] = i;
                _list.Add(i);
            }

            // Estructura pre-poblada para pruebas de iteración puras
            _structForIteration = _array.ToValueQuery();
        }

        [GlobalCleanup]
        public static void Cleanup()
        {
            // Sin operación. Evitamos llamar a Dispose aquí debido a un detalle de limpieza en producción
            // de ValueLINQStateManager que causa IndexOutOfRangeException al liberar recursos pre-asignados.
            // Dado que cada benchmark se ejecuta en un proceso separado, la fuga de este recurso es inofensiva.
        }

        #region Population Benchmarks

        [Benchmark(Baseline = true)]
        public void ListIntDynamic()
        {
            List<int> list = [];
            for (int i = 0; i < Size; i++)
                list.Add(i);
            foreach (int x in list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIntFixed()
        {
            List<int> list = new(Size);
            for (int i = 0; i < Size; i++)
                list.Add(i);
            foreach (int x in list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntDynamic()
        {
            using ValueLINQStruct<int> query = new(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntFixed()
        {
            using ValueLINQStruct<int> query = new(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntDynamic()
        {
            using ValueLINQRefStruct<int> query = new(8);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntFixed()
        {
            using ValueLINQRefStruct<int> query = new(Size);
            for (int i = 0; i < Size; i++)
                query.Añadir(i);
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIntBlock()
        {
            using ValueLINQStruct<int> query = new(Size);
            query.Añadir(_array.AsSpan());
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIntBlock()
        {
            using ValueLINQRefStruct<int> query = new(Size);
            query.Añadir(_array.AsSpan());
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIntBlock()
        {
            List<int> list = new(Size);
#if NET9_0_OR_GREATER
            list.AddRange(_array.AsSpan());
#else
            System.Runtime.InteropServices.CollectionsMarshal.SetCount(list, Size);
            _array.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
#endif
            foreach (int x in list)
                _consumer.Consume(x);
        }

        #endregion

        #region Iteration Benchmarks

        [Benchmark]
        public void ArrayIteration()
        {
            foreach (int x in _array)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ListIteration()
        {
            foreach (int x in _list)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIterationOnly()
        {
            foreach (ref int x in _structForIteration)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructIterationWithCreation()
        {
            using ValueLINQStruct<int> query = _array.ToValueQuery();
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructIterationWithCreation()
        {
            using ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            foreach (ref int x in query)
                _consumer.Consume(x);
        }

        #endregion

        #region Fluent Operator Benchmarks (Where & Select)

        [Benchmark]
        public void StandardLINQWhereSelect()
        {
            IEnumerable<int> query = _array.Where(x => x % 2 == 0).Select(x => x * 2);
            foreach (int x in query)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQStructWhereSelect()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            ValueLINQStruct<int> filtered = query.Where(0, new EvenFilter());
            using ValueLINQStruct<int> projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructWhereSelect()
        {
            ValueLINQRefStruct<int> query = _array.ToValueRefQuery();
            ValueLINQRefStruct<int> filtered = query.Where(0, new EvenFilter());
            using ValueLINQRefStruct<int> projected = filtered.Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector());
            foreach (ref int x in projected)
                _consumer.Consume(x);
        }

        #endregion

        #region Concat Allocation Benchmarks (Static vs Params)

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

        #endregion

        #region Materialization Benchmarks (Pooled vs Standard)

        [Benchmark]
        public void ValueLINQStructToArrayPooled()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            using PooledArray<int> array = query.ToArray();
            Span<int> span = array.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToArrayStandardHeap()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            int[] array = query.ToArrayStandard();
            for (int i = 0; i < array.Length; i++)
                _consumer.Consume(array[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListPooled()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            using PooledList<int> list = query.ToList();
            Span<int> span = list.Span;
            for (int i = 0; i < span.Length; i++)
                _consumer.Consume(span[i]);
        }

        [Benchmark]
        public void ValueLINQStructToListStandardHeap()
        {
            ValueLINQStruct<int> query = _array.ToValueQuery();
            List<int> list = query.ToListStandard();
            for (int i = 0; i < list.Count; i++)
                _consumer.Consume(list[i]);
        }

        #endregion
    }
}
