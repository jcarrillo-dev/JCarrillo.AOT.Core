using System.Runtime.CompilerServices;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Colecciones.Pooled
{
    public class PooledArrayTests
    {
        [Fact]
        public void ConstructorShouldInitializeCorrectly()
        {
            // Preparar y Actuar
            using PooledArray<int> array = new(10);

            // Verificar
            _ = array.Tamaño.Should().Be(10);
            _ = array.Span.Length.Should().Be(10);
            _ = array.Memory.Length.Should().Be(10);
            _ = array.IsAmpliable.Should().BeFalse();
        }

        [Fact]
        public void IndexerShouldAllowReadingAndWritingByRef()
        {
            // Preparar
            using PooledArray<int> array = new(5);

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
            PooledArray<int> array = new(5);

            try
            {
                // Actuar y Verificar
                unsafe
                {
                    nint ptr = (nint)Unsafe.AsPointer(ref array);
                    Action action1 = () => { int x = Unsafe.AsRef<PooledArray<int>>((void*)ptr)[-1]; };
                    _ = action1.Should().Throw<ArgumentOutOfRangeException>();

                    Action action2 = () => { int x = Unsafe.AsRef<PooledArray<int>>((void*)ptr)[5]; };
                    _ = action2.Should().Throw<ArgumentOutOfRangeException>();
                }
            }
            finally
            {
                array.Dispose();
            }
        }

        [Fact]
        public void DisposeShouldBeIdempotent()
        {
            // Preparar
            PooledArray<int> array = new(5);

            // Actuar y Verificar
            array.Dispose();
            _ = array.IsDisposed.Should().BeTrue();

            // Llamar a Dispose de nuevo no debería lanzar una excepción
            array.Dispose();
        }

        [Fact]
        public async Task DisposeAsyncShouldBeIdempotent()
        {
            // Preparar
            PooledArray<int> array = new(5);

            // Actuar y Verificar
            await array.DisposeAsync();
            _ = array.IsDisposed.Should().BeTrue();

            await array.DisposeAsync();
        }

        [Fact]
        public void AccessAfterDisposeShouldThrowObjectDisposedException()
        {
            // Preparar
            PooledArray<int> array = new(5);
            array.Dispose();

            // Actuar y Verificar
            unsafe
            {
                nint ptr = (nint)Unsafe.AsPointer(ref array);
                Action action1 = () => { int size = Unsafe.AsRef<PooledArray<int>>((void*)ptr).Tamaño; };
                _ = action1.Should().Throw<ObjectDisposedException>();

                Action action2 = () => { Span<int> span = Unsafe.AsRef<PooledArray<int>>((void*)ptr).Span; };
                _ = action2.Should().Throw<ObjectDisposedException>();

                Action action3 = () => { Memory<int> memory = Unsafe.AsRef<PooledArray<int>>((void*)ptr).Memory; };
                _ = action3.Should().Throw<ObjectDisposedException>();

                Action action4 = () => { int x = Unsafe.AsRef<PooledArray<int>>((void*)ptr)[0]; };
                _ = action4.Should().Throw<ObjectDisposedException>();

                Action action5 = () => Unsafe.AsRef<PooledArray<int>>((void*)ptr).Clear();
                _ = action5.Should().Throw<ObjectDisposedException>();
            }
        }
    }
}
