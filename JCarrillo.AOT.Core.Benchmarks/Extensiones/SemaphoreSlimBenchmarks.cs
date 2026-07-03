using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [MemoryDiagnoser]
    [ThreadingDiagnoser]
    [HtmlExporter]
    public class SemaphoreSlimBenchmarks : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        [GlobalSetup]
        public void Setup() => _semaphore = new SemaphoreSlim(1, 1);

        [GlobalCleanup]
        public void Cleanup() => _semaphore?.Dispose();

        public void Dispose()
        {
            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Synchronous Benchmarks

        [Benchmark(Baseline = true)]
        public void SemaphoreSlimSincrono()
        {
            _semaphore!.Wait();
            _ = _semaphore.Release();
        }

        [Benchmark]
        public void SemaphoreLockSincrono()
        {
            using SemaphoreLock l = _semaphore!.Esperar();
            // Operación bajo exclusión mutua
        }

        #endregion

        #region Asynchronous Benchmarks

        [Benchmark]
        public async Task SemaphoreSlimAsincrono()
        {
            await _semaphore!.WaitAsync().ConfigureAwait(false);
            _ = _semaphore.Release();
        }

        [Benchmark]
        public async ValueTask SemaphoreLockAsincrono()
        {
            await using SemaphoreLock l = await _semaphore!.EsperarAsync().ConfigureAwait(false);
            // Operación bajo exclusión mutua
        }

        #endregion
    }
}
