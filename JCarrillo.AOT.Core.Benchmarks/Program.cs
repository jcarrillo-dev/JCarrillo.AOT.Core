using BenchmarkDotNet.Running;

namespace JCarrillo.AOT.Core.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) => _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
