using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace JCarrillo.AOT.Core.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = ManualConfig.Create(DefaultConfig.Instance)
                .WithOptions(ConfigOptions.DisableLogFile);
            _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
