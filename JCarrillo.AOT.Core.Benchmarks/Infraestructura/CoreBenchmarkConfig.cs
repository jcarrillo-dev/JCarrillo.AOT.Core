using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
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
            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);
            AddExporter(HtmlExporter.Default);
            WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
            WithOptions(ConfigOptions.DisableLogFile);
        }
    }
}
