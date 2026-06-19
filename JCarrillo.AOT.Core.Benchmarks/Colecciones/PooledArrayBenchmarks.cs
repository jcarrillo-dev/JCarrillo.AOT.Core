using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones
{
    [MemoryDiagnoser]
    [HtmlExporter]
    public class PooledArrayBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        [Benchmark(Baseline = true)]
        public int[] StandardArray()
        {
            var arr = new int[Size];
            for (int i = 0; i < Size; i++)
            {
                arr[i] = i;
            }
            return arr;
        }

        [Benchmark]
        public int PooledArray()
        {
            using var arr = new PooledArray<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                arr[i] = i;
            }
            return arr.Tamaño;
        }
    }
}
