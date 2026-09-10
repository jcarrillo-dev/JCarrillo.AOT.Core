using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;

namespace JCarrillo.AOT.Core.Benchmarks.Infraestructura
{
    /// <summary>
    /// Configuración centralizada de BenchmarkDotNet para JCarrillo.AOT.Core.Benchmarks.
    /// Garantiza diagnósticos consistentes de memoria, hilos y ordenación unificada.
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
        }
    }
}
