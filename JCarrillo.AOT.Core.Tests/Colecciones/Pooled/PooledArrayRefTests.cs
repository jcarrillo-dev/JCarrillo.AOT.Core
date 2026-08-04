using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledArrayRefTests
    {
        [Fact]
        public void ConstructorShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using PooledArrayRef<int> array = new(10);

            // Verificar
            _ = array.Tamaño.Should().Be(10);
            _ = array.Span.Length.Should().Be(10);
            _ = array.IsAmpliable.Should().BeFalse();
            _ = array.IntentarAmpliar(20).Should().BeFalse();
        }

        [Fact]
        public void IndexerShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using PooledArrayRef<int> array = new(5);

            // Actuar
            array[2] = 42;
            ref int itemRef = ref array[2];
            itemRef = 100;

            // Verificar
            _ = array[2].Should().Be(100);
        }

        [Fact]
        public void IndexerOutOfBoundsShouldThrowArgumentOutOfRangeException()
        {
            // Preparar
            using PooledArrayRef<int> array = new(5);

            // Actuar y Verificar
            try
            {
                int x = array[-1];
                Assert.Fail("Debería haber lanzado ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                int x = array[5];
                Assert.Fail("Debería haber lanzado ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [Fact]
        public void DisposeShouldBeIdempotent()
        {
            // Preparar
            PooledArrayRef<int> array = new(5);

            // Actuar y Verificar
            array.Dispose();
            _ = array.IsDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            array.Dispose();
        }

        [Fact]
        public void AccessAfterDisposeShouldThrowObjectDisposedException()
        {
            // Preparar
            PooledArrayRef<int> array = new(5);
            array.Dispose();

            // Actuar y Verificar
            try
            {
                int size = array.Tamaño;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                Span<int> span = array.Span;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                int x = array[0];
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

        [Fact]
        public void ForeachLoopShouldWorkCorrectly()
        {
            // Preparar
            using PooledArrayRef<int> array = new(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            // Actuar & Verificar
            int sum = 0;
            int count = 0;
            foreach (int item in array)
            {
                sum += item;
                count++;
            }

            _ = sum.Should().Be(60);
            _ = count.Should().Be(3);
        }

        [Fact]
        public void ClearShouldClearTheSpan()
        {
            // Preparar
            using PooledArrayRef<int> array = new(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            // Actuar
            array.Clear();

            // Verificar
            _ = array[0].Should().Be(0);
            _ = array[1].Should().Be(0);
            _ = array[2].Should().Be(0);
        }
    }
}
