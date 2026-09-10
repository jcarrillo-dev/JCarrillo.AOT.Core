using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Metodos
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ValueLINQChunkBenchmarks
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
        public void StandardLINQChunk()
        {
            foreach (int[] chunk in _array.Chunk(16))
                for (int i = 0; i < chunk.Length; i++)
                    _consumer.Consume(chunk[i]);
        }

        [Benchmark]
        public void ValueLINQStructChunk()
        {
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = _array.ToValueQuery().Chunk(16);
            foreach (ref ValueLINQStruct<int> chunk in chunks)
                foreach (ref int x in chunk)
                    _consumer.Consume(x);
        }

        [Benchmark]
        public void ValueLINQRefStructChunk()
        {
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = _array.ToValueRefQuery().Chunk(16);
            foreach (ref ValueLINQStruct<int> chunk in chunks)
                foreach (ref int x in chunk)
                    _consumer.Consume(x);
        }
    }
}
