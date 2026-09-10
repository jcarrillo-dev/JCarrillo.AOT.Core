using BenchmarkDotNet.Running;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;

namespace JCarrillo.AOT.Core.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new CoreBenchmarkConfig());
    }
}
