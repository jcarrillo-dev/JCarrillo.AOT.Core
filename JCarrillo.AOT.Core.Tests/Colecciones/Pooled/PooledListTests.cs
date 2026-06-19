using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledListTests
    {
        [Fact]
        public void Add_ShouldAddItemsAndExpandCapacity()
        {
            // Preparar
            using var list = new PooledList<int>(2);

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
        public void Indexer_ByRef_ShouldAllowModification()
        {
            // Preparar
            using var list = new PooledList<int>(5);
            list.Add(1);

            // Actuar
            ref int itemRef = ref list[0];
            itemRef = 99;

            // Verificar
            list[0].Should().Be(99);
        }

        [Fact]
        public void Dispose_ShouldBeIdempotent()
        {
            // Preparar
            var list = new PooledList<int>(10);
            list.Add(1);

            // Actuar y Verificar
            list.Dispose();
            list.EstaDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            list.Dispose();
        }

        [Fact]
        public async Task DisposeAsync_ShouldBeIdempotent()
        {
            // Preparar
            var list = new PooledList<int>(10);
            list.Add(1);

            // Actuar y Verificar
            await list.DisposeAsync();
            list.EstaDisposed.Should().BeTrue();

            // Llamar a DisposeAsync de nuevo no debería lanzar una excepción
            await list.DisposeAsync();
        }

        [Fact]
        public void AccessAfterDispose_ShouldThrowObjectDisposedException()
        {
            // Preparar
            var list = new PooledList<int>(10);
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
                var memory = list.Memory;
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
        public void RuntimeHelpers_IsReferenceOrContainsReferences_Validation()
        {
            // Validar la lógica subyacente de clearArray en ArrayPool.Return:
            // string tiene tipo de referencia (IsReferenceOrContainsReferences = true), por lo que el pool lo limpia.
            // int es un tipo de valor sin referencias (IsReferenceOrContainsReferences = false), por lo que el pool no lo limpia.
            RuntimeHelpers.IsReferenceOrContainsReferences<string>().Should().BeTrue();
            RuntimeHelpers.IsReferenceOrContainsReferences<int>().Should().BeFalse();
        }
    }
}
