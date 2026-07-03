using FluentAssertions;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public static class AllocationAssert
    {
        public static void AssertZeroAllocations(Action action)
        {
            // Calentamiento
            action();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            long after = GC.GetAllocatedBytesForCurrentThread();

            _ = (after - before).Should().Be(0, "la acción debe ejecutarse con cero asignaciones en el montón");
        }

        public static async Task AssertZeroAllocationsAsync(Func<Task> action)
        {
            // Calentamiento
            await action();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetAllocatedBytesForCurrentThread();
            await action();
            long after = GC.GetAllocatedBytesForCurrentThread();

            _ = (after - before).Should().Be(0, "la acción asíncrona debe ejecutarse con cero asignaciones en el montón");
        }
    }
}
