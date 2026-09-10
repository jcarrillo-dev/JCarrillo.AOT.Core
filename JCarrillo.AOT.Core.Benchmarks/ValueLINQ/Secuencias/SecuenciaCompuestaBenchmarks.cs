using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura.Delegados;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class SecuenciaCompuestaBenchmarks
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
        public void StandardLINQDeepChain()
        {
            foreach (int[] chunk in _array.Where(static x => x > 10).Where(static x => (x & 1) == 0).Select(static x => x * 2).Chunk(16))
                for (int i = 0; i < chunk.Length; i++)
                    _consumer.Consume(chunk[i]);
        }

        [Benchmark]
        public void ValueLINQStructDeepChain()
        {
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = _array
                .ToValueQuery()
                .Where(static x => x > 10)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .Chunk(16);

            foreach (ref ValueLINQStruct<int> chunk in chunks)
                foreach (ref int x in chunk)
                    _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructDeepChain()
        {
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = _array
                .ToValueRefQuery()
                .Where(static x => x > 10)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .Chunk(16);

            foreach (ref ValueLINQStruct<int> chunk in chunks)
                foreach (ref int x in chunk)
                    _consumer.Consume(x);
        }
    }
}
