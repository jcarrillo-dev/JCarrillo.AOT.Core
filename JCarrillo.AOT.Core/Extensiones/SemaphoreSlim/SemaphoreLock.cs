using JCarrillo.AOT.Core.Extensiones.Boxing;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.SemaphoreSlim
{
    public readonly record struct SemaphoreLock : IDisposable
    {
        private readonly System.Threading.SemaphoreSlim _semaphore;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SemaphoreLock(System.Threading.SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            this.ValidarNoBoxeado();
            _semaphore.Release();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
