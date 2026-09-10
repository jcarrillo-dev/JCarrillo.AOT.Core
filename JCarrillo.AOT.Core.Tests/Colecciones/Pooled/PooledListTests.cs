using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using System.Runtime.CompilerServices;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledListTests
    {
        [Fact]
        public void RemoveAtDesplazaLosPosterioresYReduceElTamaño()
        {
            // Preparar
            using PooledList<int> lista = new(8);
            lista.AddRange([10, 20, 30, 40]);

            // Actuar
            lista.RemoveAt(1);

            // Verificar
            _ = lista.Tamaño.Should().Be(3);
            _ = lista.Span.ToArray().Should().Equal(10, 30, 40);
        }

        [Fact]
        public void RemoveAtDelUltimoElementoNoDesplazaNada()
        {
            // Preparar
            using PooledList<int> lista = new(8);
            lista.AddRange([10, 20, 30]);

            // Actuar
            lista.RemoveAt(2);

            // Verificar
            _ = lista.Span.ToArray().Should().Equal(10, 20);
        }

        [Fact]
        public void RemoveEliminaLaPrimeraApariciónYDevuelveSiExistia()
        {
            // Preparar
            using PooledList<int> lista = new(8);
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
        public void RemoveAtDesplazaTiposPorReferencia()
        {
            // Preparar
            using PooledList<string> lista = new(8);
            lista.AddRange(["a", "b", "c"]);

            // Actuar
            lista.RemoveAt(0);

            // Verificar
            _ = lista.Span.ToArray().Should().Equal("b", "c");
        }

        [Fact]
        public void RemoveAtFueraDeRangoLanza()
        {
            // Preparar: PooledList no se puede capturar en un lambda porque su guardián de boxing lo impide,
            // así que la aserción no puede construirse con Should().Throw().
            using PooledList<int> lista = new(4);
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
        public void AddShouldAddItemsAndExpandCapacity()
        {
            // Preparar
            using PooledList<int> list = new(2);

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
        public void IndexerByRefShouldAllowModification()
        {
            // Preparar
            using PooledList<int> list = new(5);
            list.Add(1);

            // Actuar
            ref int itemRef = ref list[0];
            itemRef = 99;

            // Verificar
            _ = list[0].Should().Be(99);
        }

        [Fact]
        public void DisposeShouldBeIdempotent()
        {
            // Preparar
            PooledList<int> list = new(10);
            list.Add(1);

            // Actuar y Verificar
            list.Dispose();
            _ = list.IsDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            list.Dispose();
        }

        [Fact]
        public async Task DisposeAsyncShouldBeIdempotent()
        {
            // Preparar
            PooledList<int> list = new(10);
            list.Add(1);

            // Actuar y Verificar
            await list.DisposeAsync();
            _ = list.IsDisposed.Should().BeTrue();

            // Llamar a DisposeAsync de nuevo no debería lanzar una excepción
            await list.DisposeAsync();
        }

        [Fact]
        public void AccessAfterDisposeShouldThrowObjectDisposedException()
        {
            // Preparar
            PooledList<int> list = new(10);
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
                Memory<int> memory = list.Memory;
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
        public void RuntimeHelpersIsReferenceOrContainsReferencesValidation()
        {
            // Validar la lógica subyacente de clearArray en ArrayPool.Return:
            // string tiene tipo de referencia (IsReferenceOrContainsReferences = true), por lo que el pool lo limpia.
            // int es un tipo de valor sin referencias (IsReferenceOrContainsReferences = false), por lo que el pool no lo limpia.
            _ = RuntimeHelpers.IsReferenceOrContainsReferences<string>().Should().BeTrue();
            _ = RuntimeHelpers.IsReferenceOrContainsReferences<int>().Should().BeFalse();
        }
    }
}
