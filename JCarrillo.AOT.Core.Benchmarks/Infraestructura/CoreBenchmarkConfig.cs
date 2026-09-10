using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;

namespace JCarrillo.AOT.Core.Benchmarks.Infraestructura
{
    /// <summary>
    /// Configuración centralizada de BenchmarkDotNet para JCarrillo.AOT.Core.Benchmarks.
    /// Garantiza diagnósticos consistentes de memoria, hilos y matriz de 6 runtimes (.NET 8, 9 y 10 JIT + NativeAOT).
    /// </summary>
    public class CoreBenchmarkConfig : ManualConfig
    {
        public CoreBenchmarkConfig()
        {
            AddLogger(ConsoleLogger.Default);
            AddColumnProvider(DefaultColumnProviders.Instance);
            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);
            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
            WithOptions(ConfigOptions.DisableLogFile);

            // Matriz de los 6 Runtimes: .NET 8, 9 y 10 (JIT y NativeAOT)
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).WithId("Net80"));
            AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net80).WithId("NativeAot80"));
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core90).WithId("Net90"));
            AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net90).WithId("NativeAot90"));
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0).WithId("Net10_0"));
            AddJob(Job.Default.WithRuntime(NativeAotRuntime.Net10_0).WithId("NativeAot10_0"));
        }
    }
}
