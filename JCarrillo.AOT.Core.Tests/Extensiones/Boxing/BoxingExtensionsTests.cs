using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.Boxing;

namespace JCarrillo.AOT.Core.Tests.Extensiones.Boxing
{
    public class BoxingExtensionsTests
    {
        private struct TestStruct
        {
            public int Value;
        }

        private sealed class HeapContainer
        {
            public TestStruct Field;
        }

        [Fact]
        public void ValidarNoBoxeadoOnStackShouldNotThrow()
        {
            // Arrange
            TestStruct val = new() { Value = 42 };

            // Act & Assert
            val.ValidarNoBoxeado(); // Should not throw because it is on the stack
        }

        [Fact]
        public void ValidarNoBoxeadoOnHeapClassFieldShouldThrowInvalidOperationException()
        {
            // Arrange
            HeapContainer container = new() { Field = new TestStruct { Value = 42 } };

            // Act
            Action act = () => container.Field.ValidarNoBoxeado();

            // Assert
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Heap*");
        }

        [Fact]
        public void ValidarNoBoxeadoOnHeapArrayShouldThrowInvalidOperationException()
        {
            // Arrange
            TestStruct[] array = [new TestStruct { Value = 42 }];

            // Act
            Action act = () => array[0].ValidarNoBoxeado();

            // Assert
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Heap*");
        }

        [Fact]
        public void ValidarNoBoxeadoOnBoxedObjectShouldThrowInvalidOperationException()
        {
            // Arrange
            object boxed = new TestStruct { Value = 42 };

            // Act
            Action act = () => Unsafe.Unbox<TestStruct>(boxed).ValidarNoBoxeado();

            // Assert
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*Heap*");
        }

        [Fact]
        public void ValidarNoBoxeadoDeepStackShouldNotThrow() => DeepStackHelper(50);

        private static void DeepStackHelper(int depth)
        {
            if (depth > 0)
            {
                DeepStackHelper(depth - 1);
            }
            else
            {
                TestStruct val = new() { Value = 42 };
                val.ValidarNoBoxeado(); // Should not throw
            }
        }

        [Fact]
        public void ValidarNoBoxeadoOnClosureCaptureShouldThrow()
        {
            TestStruct captured = new() { Value = 42 };

            // Create a closure that captures the variable
            void action() { int x = captured.Value; }
            action();

            // Now validate it (either inside or outside)
            Action act = () => captured.ValidarNoBoxeado();
            _ = act.Should().Throw<InvalidOperationException>().WithMessage("*Heap*");

            Action actInside = () => captured.ValidarNoBoxeado();
            _ = actInside.Should().Throw<InvalidOperationException>().WithMessage("*Heap*");
        }

        [Fact]
        public unsafe void ValidarNoBoxeadoOnUnmanagedHeapShouldThrow()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Unsafe.SizeOf<TestStruct>());
            try
            {
                ref TestStruct reference = ref Unsafe.AsRef<TestStruct>((void*)ptr);
                reference = new TestStruct { Value = 42 };

                IntPtr localPtr = ptr;
                Action act = () =>
                {
                    unsafe
                    {
                        ref TestStruct localRef = ref Unsafe.AsRef<TestStruct>((void*)localPtr);
                        localRef.ValidarNoBoxeado();
                    }
                };
                _ = act.Should().Throw<InvalidOperationException>();
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        [Fact]
        public void ValidarNoBoxeadoShouldAllocateZeroBytes()
        {
            TestStruct val = new() { Value = 42 };

            // Warm up
            val.ValidarNoBoxeado();

            long start = GC.GetAllocatedBytesForCurrentThread();
            val.ValidarNoBoxeado();
            long end = GC.GetAllocatedBytesForCurrentThread();

            _ = (end - start).Should().Be(0);
        }
    }
}
