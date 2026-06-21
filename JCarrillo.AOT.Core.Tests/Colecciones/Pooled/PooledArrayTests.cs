using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledArrayTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using var array = new PooledArray<int>(10);

            // Verificar
            array.Tamaño.Should().Be(10);
            array.Span.Length.Should().Be(10);
            array.Memory.Length.Should().Be(10);
            array.EsAmpliable.Should().BeFalse();
        }

        [Fact]
        public void Indexer_ShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using var array = new PooledArray<int>(5);

            // Actuar
            array[2] = 42;
            ref int itemRef = ref array[2];
            itemRef = 100;

            // Verificar
            array[2].Should().Be(100);
        }

        [Fact]
        public void Indexer_OutOfBounds_ShouldThrowIndexOutOfRangeException()
        {
            // Preparar
            using var array = new PooledArray<int>(5);

            // Actuar y Verificar
            try
            {
                var x = array[-1];
                Assert.Fail("Debería haber lanzado IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                var x = array[5];
                Assert.Fail("Debería haber lanzado IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Preparar
            var array = new PooledArray<int>(5);

            // Actuar y Verificar
            array.Dispose();
            array.EstaDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            array.Dispose();
        }

        [Fact]
        public async Task DisposeAsync_ShouldBeIdempotent()
        {
            // Preparar
            var array = new PooledArray<int>(5);

            // Actuar y Verificar
            await array.DisposeAsync();
            array.EstaDisposed.Should().BeTrue();

            await array.DisposeAsync();
        }

        [Fact]
        public void AccessAfterDispose_ShouldThrowObjectDisposedException()
        {
            // Preparar
            var array = new PooledArray<int>(5);
            array.Dispose();

            // Actuar y Verificar
            try
            {
                var size = array.Tamaño;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                var span = array.Span;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                var memory = array.Memory;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                var x = array[0];
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                array.Clear();
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }
        }
    }
}
