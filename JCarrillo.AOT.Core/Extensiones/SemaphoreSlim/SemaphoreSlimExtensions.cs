using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.SemaphoreSlim
{
    public static class SemaphoreSlimExtensions
    {
        #region Esperar (Wait)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore)
            => Esperar(semaphore, CancellationToken.None);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout)
            => Esperar(semaphore, millisecondsTimeout, CancellationToken.None);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            semaphore.Wait(cancellationToken);
            return new SemaphoreLock(semaphore);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!semaphore.Wait(millisecondsTimeout, cancellationToken))
                ThrowTimeout(millisecondsTimeout);
            return new SemaphoreLock(semaphore);
        }

        #endregion

        #region EsperarAsync (WaitAsync)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore)
            => EsperarAsync(semaphore, CancellationToken.None);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout)
            => EsperarAsync(semaphore, millisecondsTimeout, CancellationToken.None);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            if (semaphore.Wait(0, cancellationToken))
            {
                return new ValueTask<SemaphoreLock>(new SemaphoreLock(semaphore));
            }
            return EsperarAsyncSlow(semaphore, cancellationToken);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        private static async ValueTask<SemaphoreLock> EsperarAsyncSlow(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new SemaphoreLock(semaphore);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (semaphore.Wait(0, cancellationToken))
            {
                return new ValueTask<SemaphoreLock>(new SemaphoreLock(semaphore));
            }
            return EsperarAsyncSlow(semaphore, millisecondsTimeout, cancellationToken);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        private static async ValueTask<SemaphoreLock> EsperarAsyncSlow(System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!await semaphore.WaitAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false))
                ThrowTimeout(millisecondsTimeout);
            return new SemaphoreLock(semaphore);
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void ThrowTimeout(int timeout)
            => throw new TimeoutException($"No se pudo obtener el semáforo en {timeout} milisegundos.");
    }
}
