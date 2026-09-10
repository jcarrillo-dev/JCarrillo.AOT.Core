using System.Buffers;
using System.Reflection;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ArrayPoolExtensionsE2ETests
    {
        // Nivel 1: Cobertura de Funcionalidades

        [Fact]
        public void ArrayPoolExtensionsObtenerListaReturnsPooledList()
        {
            // Preparar y Actuar
            using PooledList<int> list = ArrayPool<int>.Shared.ObtenerLista(15);

            // Aserción
            _ = list.Should().BeOfType<PooledList<int>>();
            _ = list.Tamaño.Should().Be(15);
            _ = list.Span.Length.Should().Be(15);
        }

        [Fact]
        public void ArrayPoolExtensionsObtenerArregloReturnsPooledArray()
        {
            // Preparar y Actuar
            using PooledArray<int> array = ArrayPool<int>.Shared.ObtenerArreglo(20);

            // Aserción
            _ = array.Should().BeOfType<PooledArray<int>>();
            _ = array.Tamaño.Should().Be(20);
            _ = array.Span.Length.Should().Be(20);
        }

        [Fact]
        public void ArrayPoolExtensionsDisposalReturnsToPool()
        {
            // Preparar
            ArrayPool<int> arrayPool = ArrayPool<int>.Shared;

            // Actuar y Aserción
            // Rentamos y liberamos. Para verificar que retorna al pool, rentamos un tamaño grande, lo liberamos,
            // y volver a rentarlo típicamente debería retornar un arreglo limpio o el mismo sin nuevas asignaciones.
            // Como no podemos inspeccionar fácilmente el estado interno del pool, verificamos que establece IsDisposed en true.
            PooledArray<int> array = arrayPool.ObtenerArreglo(100);
            _ = array.IsDisposed.Should().BeFalse();
            array.Dispose();
            _ = array.IsDisposed.Should().BeTrue();

            PooledList<int> list = arrayPool.ObtenerLista(100);
            _ = list.IsDisposed.Should().BeFalse();
            list.Dispose();
            _ = list.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void ArrayPoolExtensionsWriteAndReadSucceeds()
        {
            // Preparar
            using PooledList<int> list = ArrayPool<int>.Shared.ObtenerLista(5);
            using PooledArray<int> array = ArrayPool<int>.Shared.ObtenerArreglo(5);

            // Actuar
            for (int i = 0; i < 5; i++)
            {
                list[i] = i * 10;
                array[i] = i * 20;
            }

            // Aserción
            for (int i = 0; i < 5; i++)
            {
                _ = list[i].Should().Be(i * 10);
                _ = array[i].Should().Be(i * 20);
            }
        }

        [Fact]
        // Actuar y Aserción
        public void ArrayPoolExtensionsZeroAllocationRentsAndDisposes() =>
            AllocationAssert.AssertZeroAllocations(() =>
            {
                using PooledList<int> list = ArrayPool<int>.Shared.ObtenerLista(10);
                using PooledArray<int> array = ArrayPool<int>.Shared.ObtenerArreglo(10);
                list[0] = 42;
                array[0] = 42;
            });

        // Nivel 2: Casos Límite y Extremos

        [Fact]
        public void ArrayPoolExtensionsObtenerListaZeroSizeSucceeds()
        {
            // Actuar y Aserción
            Action act = () =>
            {
                using PooledList<int> list = ArrayPool<int>.Shared.ObtenerLista(0);
                _ = list.Tamaño.Should().Be(0);
            };
            _ = act.Should().NotThrow();
        }

        [Fact]
        public void ArrayPoolExtensionsObtenerListaNegativeSizeThrowsException()
        {
            // Actuar y Aserción
            Action act = () => ArrayPool<int>.Shared.ObtenerLista(-5);
            _ = act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ArrayPoolExtensionsObtenerArregloNegativeSizeThrowsException()
        {
            // Actuar y Aserción
            Action act = () => ArrayPool<int>.Shared.ObtenerArreglo(-1);
            _ = act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ArrayPoolExtensionsReferenceTypeClearingClearsRentedArray()
        {
            // Preparar
            PooledArray<string> pooledArray = ArrayPool<string>.Shared.ObtenerArreglo(10);
            FieldInfo? field = typeof(PooledArray<string>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            string[] rawArray = (string[])field!.GetValue(pooledArray)!;

            rawArray[0] = "hello";
            rawArray[1] = "world";

            // Actuar
            pooledArray.Dispose();

            // Aserción
            // Los elementos del arreglo subyacente deben ser limpiados (establecidos en null) al ser devueltos al pool
            _ = rawArray[0].Should().BeNull();
            _ = rawArray[1].Should().BeNull();
        }

        [Fact]
        public async Task ArrayPoolExtensionsConcurrentRentingDoesNotCorruptPool()
        {
            // Preparar
            const int threadCount = 10;
            const int iterations = 1000;

            Task[] tasks = new Task[threadCount];

            // Actuar
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    ArrayPool<int> pool = ArrayPool<int>.Shared;
                    for (int i = 0; i < iterations; i++)
                    {
                        using PooledList<int> list = pool.ObtenerLista(5);
                        using PooledArray<int> array = pool.ObtenerArreglo(5);
                        list[0] = i;
                        array[0] = i;
                    }
                });
            }

            // Aserción
            Func<Task> act = async () => await Task.WhenAll(tasks);
            _ = await act.Should().NotThrowAsync();
        }

        [Fact]
        public void PooledListDisposeDeberiaDevolverArrayAlPoolInclusoSiFallaValidacionDeBoxing()
        {
            // Preparar
            ArrayPool<int> pool = ArrayPool<int>.Shared;
            PooledList<int> lista = pool.ObtenerLista(10);

            // Forzamos el boxing asignándolo a una variable de tipo object
            object boxedLista = lista;

            // Actuar
            Action accion = () => ((IDisposable)boxedLista).Dispose();

            // Aserción
            _ = accion.Should().Throw<InvalidOperationException>()
                .WithMessage("*boxing*");

            // A pesar de la excepción, los recursos deben haber sido liberados.
            // Para verificarlo sin depender de detalles internos inaccesibles,
            // podemos desempaquetar la estructura de la variable boxeada.
            PooledList<int> listaDesempaquetada = (PooledList<int>)boxedLista;
            _ = listaDesempaquetada.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void PooledArrayDisposeDeberiaDevolverArrayAlPoolInclusoSiFallaValidacionDeBoxing()
        {
            // Preparar
            ArrayPool<int> pool = ArrayPool<int>.Shared;
            PooledArray<int> arreglo = pool.ObtenerArreglo(10);

            // Forzamos el boxing asignándolo a una variable de tipo object
            object boxedArreglo = arreglo;

            // Actuar
            Action accion = () => ((IDisposable)boxedArreglo).Dispose();

            // Aserción
            _ = accion.Should().Throw<InvalidOperationException>()
                .WithMessage("*boxing*");

            // A pesar de la excepción, el pool debe haber recibido el arreglo y estar marcado como dispuesto.
            PooledArray<int> arregloDesempaquetado = (PooledArray<int>)boxedArreglo;
            _ = arregloDesempaquetado.IsDisposed.Should().BeTrue();
        }
    }
}
