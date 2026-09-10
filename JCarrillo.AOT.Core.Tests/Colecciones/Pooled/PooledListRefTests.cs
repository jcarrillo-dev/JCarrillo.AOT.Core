using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledListRefTests
    {
        [Fact]
        public void RemoveAtDesplazaLosPosterioresYReduceElTamaño()
        {
            // Preparar
            using PooledListRef<int> lista = new(8);
            lista.AddRange([10, 20, 30, 40]);

            // Actuar
            lista.RemoveAt(1);

            // Verificar
            _ = lista.Tamaño.Should().Be(3);
            _ = lista.Span.ToArray().Should().Equal(10, 30, 40);
        }

        [Fact]
        public void RemoveEliminaLaPrimeraApariciónYDevuelveSiExistia()
        {
            // Preparar
            using PooledListRef<int> lista = new(8);
            lista.AddRange([10, 20, 30, 20]);

            // Actuar
            bool eliminado = lista.Remove(20);
            bool inexistente = lista.Remove(99);

            // Verificar
            _ = eliminado.Should().BeTrue();
            _ = inexistente.Should().BeFalse();
            _ = lista.Span.ToArray().Should().Equal(10, 30, 20);
        }

        [Fact]
        public void RemoveAtFueraDeRangoLanza()
        {
            // Preparar: un ref struct no se puede capturar en un lambda, así que la aserción no puede
            // construirse con Should().Throw().
            using PooledListRef<int> lista = new(4);
            lista.Add(1);

            bool lanzoNegativo = false;
            bool lanzoPasado = false;

            // Actuar
            try { lista.RemoveAt(-1); } catch (ArgumentOutOfRangeException) { lanzoNegativo = true; }
            try { lista.RemoveAt(1); } catch (ArgumentOutOfRangeException) { lanzoPasado = true; }

            // Verificar
            _ = lanzoNegativo.Should().BeTrue();
            _ = lanzoPasado.Should().BeTrue();
            _ = lista.Tamaño.Should().Be(1, "un índice inválido no debe alterar la lista");
        }

        [Fact]
        public void ConstructorShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using PooledListRef<int> list = new(10);

            // Verificar
            _ = list.Tamaño.Should().Be(0);
            _ = list.Span.Length.Should().Be(0);
            _ = list.IsAmpliable.Should().BeTrue();
        }

        [Fact]
        public void AddShouldAddItemsAndExpandCapacity()
        {
            // Preparar
            using PooledListRef<int> list = new(2);

            // Actuar
            list.Add(10);
            list.Add(20);
            list.Add(30); // Desencadena la expansión

            // Verificar
            _ = list.Tamaño.Should().Be(3);
            _ = list[0].Should().Be(10);
            _ = list[1].Should().Be(20);
            _ = list[2].Should().Be(30);
            _ = list.Span.Length.Should().Be(3);
        }

        [Fact]
        public void IndexerShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using PooledListRef<int> list = new(5);
            list.Add(1);

            // Actuar
            list[0] = 42;
            ref int itemRef = ref list[0];
            itemRef = 100;

            // Verificar
            _ = list[0].Should().Be(100);
        }

        [Fact]
        public void IndexerOutOfBoundsShouldThrowArgumentOutOfRangeException()
        {
            // Preparar
            using PooledListRef<int> list = new(5);
            list.Add(10);

            // Actuar y Verificar
            try
            {
                int x = list[-1];
                Assert.Fail("Debería haber lanzado ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                int x = list[1];
                Assert.Fail("Debería haber lanzado ArgumentOutOfRangeException");
            }
            catch (ArgumentOutOfRangeException) { }
        }

        [Fact]
        public void DisposeShouldBeIdempotent()
        {
            // Preparar
            PooledListRef<int> list = new(5);
            list.Add(1);

            // Actuar y Verificar
            list.Dispose();
            _ = list.IsDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            list.Dispose();
        }

        [Fact]
        public void AccessAfterDisposeShouldThrowObjectDisposedException()
        {
            // Preparar
            PooledListRef<int> list = new(5);
            list.Dispose();

            // Actuar y Verificar
            try
            {
                int size = list.Tamaño;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                Span<int> span = list.Span;
                Assert.Fail("Debería haber lanzado ObjectDisposedException");
            }
            catch (ObjectDisposedException) { }

            try
            {
                int x = list[0];
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
        public void ForeachLoopShouldWorkCorrectly()
        {
            // Preparar
            using PooledListRef<int> list = new(5);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            // Actuar & Verificar
            int sum = 0;
            int count = 0;
            foreach (int item in list)
            {
                sum += item;
                count++;
            }

            _ = sum.Should().Be(60);
            _ = count.Should().Be(3);
        }

        [Fact]
        public void ClearShouldClearTheSpanAndResetLength()
        {
            // Preparar
            using PooledListRef<int> list = new(3);
            list.Add(10);
            list.Add(20);

            // Actuar
            list.Clear();

            // Verificar
            _ = list.Tamaño.Should().Be(0);
            _ = list.Span.Length.Should().Be(0);
        }
    }
}
