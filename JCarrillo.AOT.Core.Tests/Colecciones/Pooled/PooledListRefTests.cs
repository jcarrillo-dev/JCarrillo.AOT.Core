using System;
using Xunit;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledListRefTests
    {
        [Fact]
        public void Constructor_ShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using var list = new PooledListRef<int>(10);

            // Verificar
            list.Tamaño.Should().Be(0);
            list.Span.Length.Should().Be(0);
            list.EsAmpliable.Should().BeTrue();
        }

        [Fact]
        public void Add_ShouldAddItemsAndExpandCapacity()
        {
            // Preparar
            using var list = new PooledListRef<int>(2);

            // Actuar
            list.Add(10);
            list.Add(20);
            list.Add(30); // Desencadena la expansión

            // Verificar
            list.Tamaño.Should().Be(3);
            list[0].Should().Be(10);
            list[1].Should().Be(20);
            list[2].Should().Be(30);
            list.Span.Length.Should().Be(3);
        }

        [Fact]
        public void Indexer_ShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using var list = new PooledListRef<int>(5);
            list.Add(1);

            // Actuar
            list[0] = 42;
            ref int itemRef = ref list[0];
            itemRef = 100;

            // Verificar
            list[0].Should().Be(100);
        }

        [Fact]
        public void Indexer_OutOfBounds_ShouldThrowIndexOutOfRangeException()
        {
            // Preparar
            using var list = new PooledListRef<int>(5);
            list.Add(10);

            // Actuar y Verificar
            try
            {
                var x = list[-1];
                Assert.Fail("Debería haber lanzado IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }

            try
            {
                var x = list[1];
                Assert.Fail("Debería haber lanzado IndexOutOfRangeException");
            }
            catch (IndexOutOfRangeException) { }
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Preparar
            var list = new PooledListRef<int>(5);
            list.Add(1);

            // Actuar y Verificar
            list.Dispose();
            list.EstaDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            list.Dispose();
        }

        [Fact]
        public void AccessAfterDispose_ShouldThrowObjectDisposedException()
        {
            // Preparar
            var list = new PooledListRef<int>(5);
            list.Dispose();

            // Actuar y Verificar
            try
            {
                var size = list.Tamaño;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                var span = list.Span;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                var x = list[0];
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                list.Add(42);
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                list.Clear();
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }
        }

        [Fact]
        public void ForeachLoop_ShouldWorkCorrectly()
        {
            // Preparar
            using var list = new PooledListRef<int>(5);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            // Actuar & Verificar
            int sum = 0;
            int count = 0;
            foreach (var item in list)
            {
                sum += item;
                count++;
            }

            sum.Should().Be(60);
            count.Should().Be(3);
        }

        [Fact]
        public void Clear_ShouldClearTheSpanAndResetLength()
        {
            // Preparar
            using var list = new PooledListRef<int>(3);
            list.Add(10);
            list.Add(20);

            // Actuar
            list.Clear();

            // Verificar
            list.Tamaño.Should().Be(0);
            list.Span.Length.Should().Be(0);
        }
    }
}
