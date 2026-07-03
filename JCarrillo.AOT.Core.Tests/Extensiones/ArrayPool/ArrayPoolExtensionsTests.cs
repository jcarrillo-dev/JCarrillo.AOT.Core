using System.Buffers;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Tests.Extensiones.ArrayPool
{
    public class ArrayPoolExtensionsTests
    {
        [Fact]
        public void ObtenerListaWithValidSizeShouldReturnPooledList()
        {
            // Arrange
            ArrayPool<int> pool = ArrayPool<int>.Shared;

            // Act
            using PooledList<int> list = pool.ObtenerLista(10);

            // Assert
            _ = list.Should().NotBeNull();
            _ = list.Tamaño.Should().Be(10);
        }

        [Fact]
        public void ObtenerListaWithInvalidSizeShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            ArrayPool<int> pool = ArrayPool<int>.Shared;

            // Act
            Action act = () => pool.ObtenerLista(-1);

            // Assert
            _ = act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ObtenerArregloWithValidSizeShouldReturnPooledArray()
        {
            // Arrange
            ArrayPool<int> pool = ArrayPool<int>.Shared;

            // Act
            using PooledArray<int> array = pool.ObtenerArreglo(10);

            // Assert
            _ = array.Should().NotBeNull();
            _ = array.Tamaño.Should().Be(10);
        }

        [Fact]
        public void ObtenerArregloWithInvalidSizeShouldThrowArgumentOutOfRangeException()
        {
            // Arrange
            ArrayPool<int> pool = ArrayPool<int>.Shared;

            // Act
            Action act = () => pool.ObtenerArreglo(-1);

            // Assert
            _ = act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
