using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledArrayRefTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using var array = new PooledArrayRef<int>(10);

            // Verificar
            array.Tamaño.Should().Be(10);
            array.Span.Length.Should().Be(10);
            array.EsAmpliable.Should().BeFalse();
            PooledArrayRef<int>.IntentarAmpliar(20).Should().BeFalse();
        }

        [Fact]
        public void Indexer_ShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using var array = new PooledArrayRef<int>(5);

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
            using var array = new PooledArrayRef<int>(5);

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
            var array = new PooledArrayRef<int>(5);

            // Actuar y Verificar
            array.Dispose();
            array.EstaDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            array.Dispose();
        }

        [Fact]
        public void AccessAfterDispose_ShouldThrowObjectDisposedException()
        {
            // Preparar
            var array = new PooledArrayRef<int>(5);
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

        [Fact]
        public void ForeachLoop_ShouldWorkCorrectly()
        {
            // Preparar
            using var array = new PooledArrayRef<int>(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            // Actuar & Verificar
            int sum = 0;
            int count = 0;
            foreach (var item in array)
            {
                sum += item;
                count++;
            }

            sum.Should().Be(60);
            count.Should().Be(3);
        }

        [Fact]
        public void Clear_ShouldClearTheSpan()
        {
            // Preparar
            using var array = new PooledArrayRef<int>(3);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;

            // Actuar
            array.Clear();

            // Verificar
            array[0].Should().Be(0);
            array[1].Should().Be(0);
            array[2].Should().Be(0);
        }
    }
}
