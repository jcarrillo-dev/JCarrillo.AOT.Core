using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArrayPoolBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public void StandardArrayPoolRentReturn()
        {
            int[] buffer = ArrayPool<int>.Shared.Rent(Size);
            for (int i = 0; i < Size; i++)
                buffer[i] = i;
            for (int i = 0; i < Size; i++)
                _consumer.Consume(buffer[i]);
            ArrayPool<int>.Shared.Return(buffer);
        }

        [Benchmark]
        public void ArrayPoolObtenerArreglo()
        {
            using PooledArray<int> array = ArrayPool<int>.Shared.ObtenerArreglo(Size);
            for (int i = 0; i < Size; i++)
                array[i] = i;
            for (int i = 0; i < Size; i++)
                _consumer.Consume(array[i]);
        }

        [Benchmark]
        public void ArrayPoolObtenerLista()
        {
            using PooledList<int> list = ArrayPool<int>.Shared.ObtenerLista(Size);
            for (int i = 0; i < Size; i++)
                list[i] = i;
            for (int i = 0; i < Size; i++)
                _consumer.Consume(list[i]);
        }
    }
}
