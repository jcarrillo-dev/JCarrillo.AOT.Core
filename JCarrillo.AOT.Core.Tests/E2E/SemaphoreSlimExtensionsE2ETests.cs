using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;


namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class SemaphoreSlimExtensionsE2ETests
    {
        // Tier 1: Feature Coverage

        [Fact]
        public void SemaphoreLockWaitAcquiresLock()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Act
            using SemaphoreLock l = semaphore.Esperar();

            // Assert
            _ = semaphore.CurrentCount.Should().Be(0);
            _ = semaphore.Wait(0).Should().BeFalse("the lock is already acquired");
        }

        [Fact]
        public async Task SemaphoreLockWaitAsyncAcquiresLock()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Act
            await using SemaphoreLock l = await semaphore.EsperarAsync();

            // Assert
            _ = semaphore.CurrentCount.Should().Be(0);
            _ = semaphore.Wait(0).Should().BeFalse("the lock is already acquired");
        }

        [Fact]
        public void SemaphoreLockDisposeReleasesLock()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Act
            SemaphoreLock l = semaphore.Esperar();
            _ = semaphore.CurrentCount.Should().Be(0);
            l.Dispose();

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task SemaphoreLockDisposeAsyncReleasesLock()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Act
            SemaphoreLock l = await semaphore.EsperarAsync();
            _ = semaphore.CurrentCount.Should().Be(0);
            await l.DisposeAsync();

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public void SemaphoreLockUsingBlockCleansUpSucceeds()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Act
            try
            {
                using (semaphore.Esperar())
                {
                    _ = semaphore.CurrentCount.Should().Be(0);
                    throw new InvalidOperationException("Simulated inner error");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated inner error")
            {
                // Expected exception
            }

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        // Tier 2: Boundary & Corner

        [Fact]
        public void SemaphoreLockDoubleDisposeIsSafeNoOp()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);
            SemaphoreLock l = semaphore.Esperar();

            // Act
            l.Dispose();
            l.Dispose(); // Second dispose should be a no-op

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task SemaphoreLockDoubleDisposeAsyncIsSafeNoOp()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);
            SemaphoreLock l = await semaphore.EsperarAsync();

            // Act
            await l.DisposeAsync();
            await l.DisposeAsync(); // Second dispose should be a no-op

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task SemaphoreLockMixedDoubleDisposeIsSafeNoOp()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // Case 1: Dispose then DisposeAsync
            await ExecuteCase1(semaphore);

            // Case 2: DisposeAsync then Dispose
            await ExecuteCase2(semaphore);
        }

        private static ValueTask ExecuteCase1(SemaphoreSlim semaphore)
        {
            SemaphoreLock l1 = semaphore.Esperar();
            l1.Dispose();
            return l1.DisposeAsync();
        }

        private static ValueTask ExecuteCase2(SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            SemaphoreLock l2 = new(semaphore);
            ValueTask vt = l2.DisposeAsync();
            l2.Dispose();
            return vt;
        }

        [Fact]
        public async Task SemaphoreLockConcurrentDisposalReleasesExactlyOnce()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);
            SemaphoreLock l = semaphore.Esperar();
            const int threadCount = 10;
            Task[] tasks = new Task[threadCount];

            // Act
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(async () => await l.DisposeAsync());
            }
            await Task.WhenAll(tasks);

            // Assert
            _ = semaphore.CurrentCount.Should().Be(1);
        }

        [Fact]
        public async Task SemaphoreLockConcurrentMixedDisposalReleasesExactlyOnce()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);

            // We run the stress test multiple times to increase the probability of intercepting race conditions.
            for (int run = 0; run < 50; run++)
            {
                // Reset semaphore if needed
                if (semaphore.CurrentCount == 0)
                {
                    _ = semaphore.Release();
                }

                RunConcurrentDisposalIteration(semaphore);

                // Assert: The semaphore must have been released exactly once, so count is 1.
                _ = semaphore.CurrentCount.Should().Be(1);
            }
            await Task.CompletedTask;
        }

        private static void RunConcurrentDisposalIteration(SemaphoreSlim semaphore)
        {
            SemaphoreLock l = semaphore.Esperar();
            IntPtr ptr;
            unsafe
            {
                ptr = (IntPtr)Unsafe.AsPointer(ref l);
            }

            const int threadCount = 20;
            Task[] tasks = new Task[threadCount];
            Barrier barrier = new(threadCount + 1);

            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();

                    // Jitter to maximize overlap
                    Thread.Sleep(Random.Shared.Next(1, 3));

                    if (index % 2 == 0)
                    {
                        try
                        {
                            unsafe
                            {
                                ref SemaphoreLock lockRef = ref Unsafe.AsRef<SemaphoreLock>((void*)ptr);
                                lockRef.Dispose();
                            }
                            Assert.Fail("Should have thrown InvalidOperationException");
                        }
                        catch (InvalidOperationException ex)
                        {
                            _ = ex.Message.Should().Contain("SemaphoreLock");
                        }
                    }
                    else
                    {
                        unsafe
                        {
                            ref SemaphoreLock lockRef = ref Unsafe.AsRef<SemaphoreLock>((void*)ptr);
                            return lockRef.DisposeAsync().AsTask();
                        }
                    }
                    return Task.CompletedTask;
                });
            }

            barrier.SignalAndWait();

            // Main thread also participates by calling Dispose() on its own stack-allocated instance.
            // This should NOT throw because it is on the owner thread's stack.
            l.Dispose();

            Task.WaitAll(tasks);
        }

        [Fact]
        public async Task SemaphoreLockWaitWithCancelledTokenThrowsImmediately()
        {
            // Arrange
            using SemaphoreSlim semaphore = new(1, 1);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert
            Action actWait = () => semaphore.Esperar(cts.Token);
            _ = actWait.Should().Throw<OperationCanceledException>();

            Func<Task> actWaitAsync = async () => await semaphore.EsperarAsync(cts.Token);
            _ = await actWaitAsync.Should().ThrowAsync<OperationCanceledException>();

            _ = semaphore.CurrentCount.Should().Be(1);
        }

        // Tier 4: Real-World Application Scenarios

        [Fact]
        public async Task ScenarioDatabaseConnectionPoolGuardSucceeds()
        {
            // Simulate a connection pool of 10 connections throttled by a SemaphoreSlim
            using SemaphoreSlim semaphore = new(10, 10);
            const int taskCount = 100;
            Task[] tasks = new Task[taskCount];
            int activeConnections = 0;
            int maxActiveConnections = 0;
            object lockObj = new();

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                int id = i;
                tasks[i] = Task.Run(async () =>
                {
                    try
                    {
                        await using (await semaphore.EsperarAsync())
                        {
                            try
                            {
                                int current;
                                lock (lockObj)
                                {
                                    activeConnections++;
                                    if (activeConnections > maxActiveConnections)
                                    {
                                        maxActiveConnections = activeConnections;
                                    }
                                    current = activeConnections;
                                }

                                _ = current.Should().BeLessThanOrEqualTo(10);

                                // Simulate connection work
                                await Task.Delay(5);

                                // Occasionally throw exception
                                if (id % 10 == 0)
                                {
                                    throw new InvalidOperationException("Simulated connection failure");
                                }
                            }
                            finally
                            {
                                lock (lockObj)
                                {
                                    activeConnections--;
                                }
                            }
                        }
                    }
                    catch (InvalidOperationException ex) when (ex.Message == "Simulated connection failure")
                    {
                        // Expected exception
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Assert
            _ = semaphore.CurrentCount.Should().Be(10);
            _ = maxActiveConnections.Should().BeLessThanOrEqualTo(10);
            _ = activeConnections.Should().Be(0);
        }
    }
}
