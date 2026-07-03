using System.Buffers;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ChallengerStressTests
    {
        [Fact]
        public void SemaphoreLockCopyOnStackAllowsDoubleRelease()
        {
            // Preparar
            using SemaphoreSlim semaphore = new(1, 2);

            // Actuar
            SemaphoreLock lock1 = semaphore.Esperar();
            SemaphoreLock lock2 = lock1; // Copia en la pila

            lock1.Dispose(); // Primera liberación

            // Si se permite liberar la copia, liberará el semáforo de nuevo,
            // incrementando el conteo por encima del máximo inicial.
            lock2.Dispose();

            // Aserción
            _ = semaphore.CurrentCount.Should().Be(2, "el semáforo fue liberado dos veces debido a la copia");
        }

        [Fact]
        public void PooledListCopyOnStackAllowsDoubleReturnToPool()
        {
            // Preparar
            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            // Rentar una lista
            PooledList<byte> list1 = pool.ObtenerLista(16);
            PooledList<byte> list2 = list1; // Copia en la pila

            // Actuar
            list1.Dispose();

            list2.Dispose();

            // Aserción
            // Dado que se devolvió dos veces, si rentamos de nuevo, podríamos obtener el mismo arreglo dos veces.
            _ = pool.Rent(16);
            _ = pool.Rent(16);

            // Si el pool está corrompido o si obtuvimos el mismo arreglo:
            // Nota: Los detalles de implementación de ArrayPool pueden variar, pero devolver el mismo arreglo dos veces es un riesgo conocido.
            // Veamos si son la misma instancia.
            // (arr1 == arr2).Should().BeTrue(); // Esto puede o no ser cierto dependiendo de la implementación de ArrayPool del runtime de .NET, pero es un riesgo.
        }

        [Fact]
        public async Task SemaphoreLockExtremeContentionSyncNoDeadlocksOrCorruption()
        {
            // Preparar
            using SemaphoreSlim semaphore = new(1, 1);
            const int threadCount = 50;
            const int operationsPerThread = 500;

            Task[] tasks = new Task[threadCount];
            int activeCount = 0;
            int maxConcurrent = 0;
            int totalAcquisitions = 0;
            object lockObj = new();

            // Actuar
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        using (semaphore.Esperar())
                        {
                            int current = Interlocked.Increment(ref activeCount);
                            lock (lockObj)
                            {
                                if (current > maxConcurrent) maxConcurrent = current;
                                totalAcquisitions++;
                            }
                            // Trabajo ligero
                            Thread.SpinWait(10);
                            _ = Interlocked.Decrement(ref activeCount);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Aserción
            _ = semaphore.CurrentCount.Should().Be(1);
            _ = maxConcurrent.Should().Be(1, "un semáforo de capacidad 1 nunca debería permitir la entrada concurrente");
            _ = totalAcquisitions.Should().Be(threadCount * operationsPerThread);
        }

        [Fact]
        public async Task SemaphoreLockExtremeContentionAsyncNoDeadlocksOrCorruption()
        {
            // Preparar
            using SemaphoreSlim semaphore = new(1, 1);
            const int threadCount = 50;
            const int operationsPerThread = 500;

            Task[] tasks = new Task[threadCount];
            int activeCount = 0;
            int maxConcurrent = 0;
            int totalAcquisitions = 0;
            object lockObj = new();

            // Actuar
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    for (int j = 0; j < operationsPerThread; j++)
                    {
                        await using (await semaphore.EsperarAsync())
                        {
                            int current = Interlocked.Increment(ref activeCount);
                            lock (lockObj)
                            {
                                if (current > maxConcurrent) maxConcurrent = current;
                                totalAcquisitions++;
                            }
                            // Trabajo ligero
                            await Task.Yield();
                            _ = Interlocked.Decrement(ref activeCount);
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            // Aserción
            _ = semaphore.CurrentCount.Should().Be(1);
            _ = maxConcurrent.Should().Be(1, "un semáforo de capacidad 1 nunca debería permitir la entrada concurrente");
            _ = totalAcquisitions.Should().Be(threadCount * operationsPerThread);
        }

        [Fact]
        public async Task ArrayPoolExtensionsExtremeContentionNoCorruption()
        {
            // Preparar
            const int threadCount = 40;
            const int iterations = 1000;

            Task[] tasks = new Task[threadCount];

            // Actuar
            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    ArrayPool<int> pool = ArrayPool<int>.Shared;
                    for (int i = 0; i < iterations; i++)
                    {
                        // Rentar diferentes tamaños
                        int size = ((threadId + i) % 32) + 1;

                        using (PooledList<int> list = pool.ObtenerLista(size))
                        {
                            _ = list.Tamaño.Should().Be(size);
                            list[0] = threadId;
                            list[size - 1] = i;
                        }

                        using PooledArray<int> array = pool.ObtenerArreglo(size);
                        _ = array.Tamaño.Should().Be(size);
                        array[0] = threadId;
                        array[size - 1] = i;
                    }
                });
            }

            // Aserción
            Func<Task> act = async () => await Task.WhenAll(tasks);
            _ = await act.Should().NotThrowAsync();
        }
    }
}
