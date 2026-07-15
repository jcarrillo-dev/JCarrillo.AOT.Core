using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.Extensiones.Boxing;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class BoxingExtensionsE2ETests
    {
        private struct TestStruct
        {
            public int Value;
        }

        private sealed class HeapContainer
        {
            public TestStruct Field;
        }

        private ref struct TestRefStruct
        {
            public TestStruct Inner;
        }

        // Nivel 1: Cobertura de Características

        [Fact]
        public void BoxingExtensionsValidarNoBoxeadoSucceedsOnStack()
        {
            // Preparar y Actuar
            TestStruct val = new() { Value = 42 };

            // Aserción
            val.ValidarNoBoxeado();
        }

        [Fact]
        public void BoxingExtensionsValidarNoBoxeadoThrowsOnHeap()
        {
            // Preparar 1: Objeto boxeado en el montón
            object boxed = new TestStruct { Value = 42 };

            // Actuar y Aserción 1
            Action act1 = () => Unsafe.Unbox<TestStruct>(boxed).ValidarNoBoxeado();
            _ = act1.Should().Throw<InvalidOperationException>()
                .WithMessage("*boxing*");

            // Preparar 2: Campo de una clase en el montón
            HeapContainer container = new() { Field = new TestStruct { Value = 42 } };

            // Actuar y Aserción 2
            Action act2 = () => container.Field.ValidarNoBoxeado();
            _ = act2.Should().Throw<InvalidOperationException>()
                .WithMessage("*boxing*");
        }

        [Fact]
        public void BoxingExtensionsWindowsLimitsAreCorrect()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // Solo se ejecuta en Windows

            // Preparar
            TestStruct val = new();
            val.ValidarNoBoxeado();

            FieldInfo? lowField = typeof(BoxingExtensions).GetField("_stackLow", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo? highField = typeof(BoxingExtensions).GetField("_stackHigh", BindingFlags.NonPublic | BindingFlags.Static);

            // Actuar
            nuint low = (nuint)lowField!.GetValue(null)!;
            nuint high = (nuint)highField!.GetValue(null)!;

            // Aserción
            _ = low.Should().BeGreaterThan(0);
            _ = high.Should().BeGreaterThan(low);

            unsafe
            {
                nuint ptr = (nuint)Unsafe.AsPointer(ref val);
                _ = ptr.Should().BeInRange(low, high);
            }
        }

        [Fact]
        public void BoxingExtensionsLinuxLimitsAreCorrect()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // Solo se ejecuta en Linux/macOS

            // Preparar
            TestStruct val = new();

            // Actuar y Aserción
            Action act = () => val.ValidarNoBoxeado();
            _ = act.Should().NotThrow();
        }

        [Fact]
        public void SemaphoreLockDisposeValidatesNoBoxing()
        {
            // Preparar
            using SemaphoreSlim semaphore = new(1, 1);
            semaphore.Wait(); // Adquirimos el bloqueo para evitar SemaphoreFullException al hacer Dispose
            object boxedLock = new SemaphoreLock(semaphore);

            // Actuar y Aserción
            Action act = () => Unsafe.Unbox<SemaphoreLock>(boxedLock).Dispose();
            _ = act.Should().Throw<InvalidOperationException>()
                .WithMessage("*boxing*");
        }

        // Nivel 2: Casos Límite y Extremos

        [Fact]
        public void BoxingExtensionsLinuxReducedStackDoesNotFalsePositive()
        {
            // Preparar
            TestStruct val = new();

            FieldInfo? lowField = typeof(BoxingExtensions).GetField("_stackLow", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo? highField = typeof(BoxingExtensions).GetField("_stackHigh", BindingFlags.NonPublic | BindingFlags.Static);

            // Guardar valores originales
            nuint origLow = (nuint)lowField!.GetValue(null)!;
            nuint origHigh = (nuint)highField!.GetValue(null)!;

            Exception? excepcionCapturada = null;

            try
            {
                // Simular un stack muy ajustado alrededor de la variable
                unsafe
                {
                    nuint ptr = (nuint)Unsafe.AsPointer(ref val);
                    lowField.SetValue(null, ptr - 100);
                    highField.SetValue(null, ptr + 100);
                }

                // Actuar (sin lambda: una captura izaría 'val' al heap y el GC podría moverla fuera de la ventana)
                try
                {
                    val.ValidarNoBoxeado();
                }
                catch (Exception ex)
                {
                    excepcionCapturada = ex;
                }
            }
            finally
            {
                // Restaurar valores originales
                lowField.SetValue(null, origLow);
                highField.SetValue(null, origHigh);
            }

            // Aserción
            _ = excepcionCapturada.Should().BeNull();
        }

        [Fact]
        public void BoxingExtensionsNestedStructOnStackValidatesSuccessfully()
        {
            // Preparar
            var outer = new { Inner = new TestStruct { Value = 123 } };
            TestStruct inner = outer.Inner;

            // Actuar y Aserción
            inner.ValidarNoBoxeado();
        }

        [Fact]
        public void BoxingExtensionsStructInRefStructValidatesSuccessfully()
        {
            // Preparar
            TestRefStruct refStruct = new() { Inner = new TestStruct { Value = 456 } };

            // Actuar y Aserción
            refStruct.Inner.ValidarNoBoxeado();
        }

        [Fact]
        public async Task BoxingExtensionsAsyncStateMachineAvoidsFalsePositives()
        {
            // Preparar
            using SemaphoreSlim semaphore = new(1, 1);
            await semaphore.WaitAsync();
            object boxedLock = new SemaphoreLock(semaphore);

            // Actuar y Aserción
            // DisposeAsync no realiza validación de boxing, por lo que no debería lanzar excepción
            // incluso si está boxeado en el montón.
            Func<Task> act = async () => await Unsafe.Unbox<SemaphoreLock>(boxedLock).DisposeAsync();
            _ = await act.Should().NotThrowAsync();
        }

        [Fact]
        // Actuar y Aserción
        public void BoxingExtensionsDeepStackFrameValidatesSuccessfully() =>
            RunRecursive(500); // Prueba de esfuerzo con una pila de llamadas muy profunda (500 marcos)

        private static void RunRecursive(int depth)
        {
            TestStruct val = new() { Value = depth };
            val.ValidarNoBoxeado();

            if (depth > 0)
            {
                RunRecursive(depth - 1);
            }
        }

        [Fact]
        public void BoxingExtensionsHeapArrayElementThrowsInvalidOperationException()
        {
            // Preparar
            TestStruct[] array = new TestStruct[10];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new TestStruct { Value = i };
            }

            // Actuar
            // En C#, los elementos de un arreglo residen en el montón (heap), por lo que pasar un elemento
            // de arreglo por referencia apunta a una ubicación del montón. Esto debe detectarse y lanzar excepción.
            Action act = () => array[5].ValidarNoBoxeado();

            // Aserción
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*boxing*");
        }

        [Fact]
        public void BoxingExtensionsClosureCaptureThrowsInvalidOperationException()
        {
            // Preparar
            TestStruct val = new() { Value = 42 };

            // Definir una función local o clausura que capture 'val',
            // causando que el compilador promueva 'val' a una clase de visualización (display class) asignada en el montón.
            void closure() => val.Value = 99;

            // Actuar y Aserción
            // Dado que 'val' se promueve al montón, llamar a ValidarNoBoxeado sobre él debe lanzar excepción.
            Action act = () => val.ValidarNoBoxeado();
            _ = act.Should().Throw<InvalidOperationException>()
               .WithMessage("*boxing*");

            // Ejecutamos la clausura para evitar que el compilador la optimice eliminándola
            closure();
        }

        [Fact]
        // Preparar, Actuar y Aserción
        public void BoxingExtensionsValidarNoBoxeadoAllocatesZeroBytes() =>
            AllocationAssert.AssertZeroAllocations(() =>
            {
                TestStruct val = new() { Value = 42 };
                val.ValidarNoBoxeado();
            });
    }
}
