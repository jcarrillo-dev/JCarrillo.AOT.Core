using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Extensiones.Spans;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class SpanBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _sourceArray = null!;
        private int[] _workingBuffer = null!;
        private readonly Consumer _consumer = new();

        [GlobalSetup]
        public void Setup()
        {
            _sourceArray = new int[Size];
            _workingBuffer = new int[Size];
            for (int i = 0; i < Size; i++)
                _sourceArray[i] = i;
        }

        [IterationSetup]
        public void IterationSetup()
            => Array.Copy(_sourceArray, _workingBuffer, Size);

        [Benchmark(Baseline = true)]
        public void StandardArrayCopyShift()
        {
            int index = Size / 2;
            int siguiente = index + 1;
            if (siguiente < _workingBuffer.Length)
                Array.Copy(_workingBuffer, siguiente, _workingBuffer, index, _workingBuffer.Length - siguiente);
            _consumer.Consume(_workingBuffer);
        }

        [Benchmark]
        public void SpanEliminarEnIndice()
        {
            int index = Size / 2;
            _workingBuffer.AsSpan().EliminarEnIndice(index);
            _consumer.Consume(_workingBuffer);
        }
    }
}
