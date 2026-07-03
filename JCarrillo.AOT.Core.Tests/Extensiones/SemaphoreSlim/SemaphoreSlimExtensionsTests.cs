using FluentAssertions;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Extensiones.SemaphoreSlim
{
    public class SemaphoreSlimExtensionsTests
    {
        [Fact]
        public void EsperarShouldAcquireLockAndDisposeShouldRelease()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);

            // Act
            using (SemaphoreLock l = semaphore.Esperar())
            {
                _ = semaphore.CurrentCount.Should().Be(0);
            }

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task EsperarAsyncShouldAcquireLockAndDisposeShouldRelease()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);

            // Act
            await using (SemaphoreLock l = await semaphore.EsperarAsync())
            {
                _ = semaphore.CurrentCount.Should().Be(0);
            }

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public void EsperarTimeoutShouldThrowTimeoutException()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(0, 1);

            // Act & Assert
            Action act = () => semaphore.Esperar(10);
            _ = act.Should().Throw<TimeoutException>()
               .WithMessage("No se pudo obtener el semáforo en 10 milisegundos.");
        }

        [Fact]
        public async Task EsperarAsyncTimeoutShouldThrowTimeoutException()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(0, 1);

            // Act & Assert
            Func<Task> act = async () => await semaphore.EsperarAsync(10);
            _ = await act.Should().ThrowAsync<TimeoutException>()
               .WithMessage("No se pudo obtener el semáforo en 10 milisegundos.");
        }

        [Fact]
        public void EsperarCancellationShouldThrowOperationCanceledException()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(0, 1);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert
            Action act = () => semaphore.Esperar(cts.Token);
            _ = act.Should().Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task EsperarAsyncCancellationShouldThrowOperationCanceledException()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(0, 1);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert
            Func<Task> act = async () => await semaphore.EsperarAsync(cts.Token);
            _ = await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public void EsperarFastPathShouldAllocateZeroBytes()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);

            // Warm up
            {
                using SemaphoreLock l = semaphore.Esperar();
            }

            // Act
            long startAllocated = GC.GetAllocatedBytesForCurrentThread();
            using (SemaphoreLock l2 = semaphore.Esperar())
            {
            }
            long endAllocated = GC.GetAllocatedBytesForCurrentThread();

            long allocated = endAllocated - startAllocated;
            _ = allocated.Should().Be(0, "acquiring and disposing the SemaphoreLock synchronously should allocate zero bytes");
        }

        [Fact]
        public async Task EsperarAsyncFastPathShouldAllocateZeroBytes()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);

            // Warm up
            {
                await using SemaphoreLock l = await semaphore.EsperarAsync();
            }

            // Act
            long startAllocated = GC.GetAllocatedBytesForCurrentThread();
            await using (SemaphoreLock l2 = await semaphore.EsperarAsync())
            {
            }
            long endAllocated = GC.GetAllocatedBytesForCurrentThread();

            long allocated = endAllocated - startAllocated;
            _ = allocated.Should().Be(0, "acquiring and disposing the SemaphoreLock asynchronously (fast path) should allocate zero bytes");
        }

        [Fact]
        public async Task ConcurrencyShouldSerializeExecution()
        {
            // Arrange
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);
            int counter = 0;

            // Act
            Task task1 = Task.Run(async () =>
            {
                await using SemaphoreLock l = await semaphore.EsperarAsync();
                await Task.Delay(50);
                _ = Interlocked.Increment(ref counter);
            });

            Task task2 = Task.Run(async () =>
            {
                await Task.Delay(10); // Ensure task1 starts first
                await using SemaphoreLock l = await semaphore.EsperarAsync();
                _ = counter.Should().Be(1, "task 1 should have run first");
                _ = Interlocked.Increment(ref counter);
            });

            await Task.WhenAll(task1, task2);

            // Assert
            _ = counter.Should().Be(2);
        }

        [Fact]
        public void EsperarBoxedToIDisposableShouldThrowInvalidOperationException()
        {
            using System.Threading.SemaphoreSlim semaphore = new(1, 1);
            SemaphoreLock l = semaphore.Esperar();
            IDisposable disposable = ForceBoxing(l); // Forzamos boxing al castear a interfaz

            Action act = disposable.Dispose;
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("Error: Se ha detectado boxing o ubicación en el Heap para el struct SemaphoreLock. Su uso está estrictamente restringido a la pila (Stack).");
        }

        private static IDisposable ForceBoxing(object obj) => (IDisposable)obj;
    }
}
