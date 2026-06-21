using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [MemoryDiagnoser]
    [HtmlExporter]
    public class SemaphoreSlimBenchmarks
    {
        private System.Threading.SemaphoreSlim? _semaphore;

        [IterationSetup]
        public void Setup()
        {
            _semaphore = new System.Threading.SemaphoreSlim(1, 1);
        }

        [IterationCleanup]
        public void Cleanup()
        {
            _semaphore?.Dispose();
        }

        #region Synchronous Benchmarks

        [Benchmark(Baseline = true)]
        public void SemaphoreSlim_Sincrono()
        {
            _semaphore!.Wait();
            _semaphore.Release();
        }

        [Benchmark]
        public void SemaphoreLock_Sincrono()
        {
            using var l = _semaphore!.Esperar();
            // Operación bajo exclusión mutua
        }

        #endregion

        #region Asynchronous Benchmarks

        [Benchmark]
        public async Task SemaphoreSlim_Asincrono()
        {
            await _semaphore!.WaitAsync().ConfigureAwait(false);
            _semaphore.Release();
        }

        [Benchmark]
        public async ValueTask SemaphoreLock_Asincrono()
        {
            await using var l = await _semaphore!.EsperarAsync().ConfigureAwait(false);
            // Operación bajo exclusión mutua
        }

        #endregion
    }
}
