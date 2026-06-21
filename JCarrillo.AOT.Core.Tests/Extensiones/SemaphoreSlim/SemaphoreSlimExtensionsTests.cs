using FluentAssertions;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Extensiones.SemaphoreSlim
{
    public class SemaphoreSlimExtensionsTests
    {
        [Fact]
        public void Esperar_ShouldAcquireLockAndDisposeShouldRelease()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);

            // Act
            using (var l = semaphore.Esperar())
            {
                semaphore.CurrentCount.Should().Be(0);
            }

            // Assert
            semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task EsperarAsync_ShouldAcquireLockAndDisposeShouldRelease()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);

            // Act
            await using (var l = await semaphore.EsperarAsync())
            {
                semaphore.CurrentCount.Should().Be(0);
            }

            // Assert
            semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public void Esperar_Timeout_ShouldThrowTimeoutException()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(0, 1);

            // Act & Assert
            Action act = () => semaphore.Esperar(10);
            act.Should().Throw<TimeoutException>()
               .WithMessage("No se pudo obtener el semáforo en 10 milisegundos.");
        }

        [Fact]
        public async Task EsperarAsync_Timeout_ShouldThrowTimeoutException()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(0, 1);

            // Act & Assert
            Func<Task> act = async () => await semaphore.EsperarAsync(10);
            await act.Should().ThrowAsync<TimeoutException>()
               .WithMessage("No se pudo obtener el semáforo en 10 milisegundos.");
        }

        [Fact]
        public void Esperar_Cancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(0, 1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Action act = () => semaphore.Esperar(cts.Token);
            act.Should().Throw<OperationCanceledException>();
        }

        [Fact]
        public async Task EsperarAsync_Cancellation_ShouldThrowOperationCanceledException()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(0, 1);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            Func<Task> act = async () => await semaphore.EsperarAsync(cts.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public void Esperar_FastPath_ShouldAllocateZeroBytes()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);

            // Warm up
            {
                using var l = semaphore.Esperar();
            }

            // Act
            long startAllocated = GC.GetAllocatedBytesForCurrentThread();
            using (var l2 = semaphore.Esperar())
            {
            }
            long endAllocated = GC.GetAllocatedBytesForCurrentThread();

            long allocated = endAllocated - startAllocated;
            allocated.Should().Be(0, "acquiring and disposing the SemaphoreLock synchronously should allocate zero bytes");
        }

        [Fact]
        public async Task EsperarAsync_FastPath_ShouldAllocateZeroBytes()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);

            // Warm up
            {
                await using var l = await semaphore.EsperarAsync();
            }

            // Act
            long startAllocated = GC.GetAllocatedBytesForCurrentThread();
            await using (var l2 = await semaphore.EsperarAsync())
            {
            }
            long endAllocated = GC.GetAllocatedBytesForCurrentThread();

            long allocated = endAllocated - startAllocated;
            allocated.Should().Be(0, "acquiring and disposing the SemaphoreLock asynchronously (fast path) should allocate zero bytes");
        }

        [Fact]
        public async Task Concurrency_ShouldSerializeExecution()
        {
            // Arrange
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);
            int counter = 0;

            // Act
            var task1 = Task.Run(async () =>
            {
                await using var l = await semaphore.EsperarAsync();
                await Task.Delay(50);
                Interlocked.Increment(ref counter);
            });

            var task2 = Task.Run(async () =>
            {
                await Task.Delay(10); // Ensure task1 starts first
                await using var l = await semaphore.EsperarAsync();
                counter.Should().Be(1, "task 1 should have run first");
                Interlocked.Increment(ref counter);
            });

            await Task.WhenAll(task1, task2);

            // Assert
            counter.Should().Be(2);
        }

        [Fact]
        public void Esperar_BoxedToIDisposable_ShouldThrowInvalidOperationException()
        {
            using var semaphore = new System.Threading.SemaphoreSlim(1, 1);
            var l = semaphore.Esperar();
#pragma warning disable CA1859 // Diseñado deliberadamente para forzar el boxing y validar la excepción en tiempo de ejecución
            IDisposable disposable = l; // Forzamos boxing al castear a interfaz
#pragma warning restore

            Action act = () => disposable.Dispose();
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Error: Se ha detectado boxing o ubicación en el Heap para el struct SemaphoreLock. Su uso está estrictamente restringido a la pila (Stack).");
        }
    }
}
