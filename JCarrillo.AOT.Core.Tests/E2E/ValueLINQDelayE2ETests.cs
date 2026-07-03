#if NET9_0_OR_GREATER
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ValueLINQDelayE2ETests
    {
        private static readonly int[] array123 = [1, 2, 3];
        private sealed class ExecutionState
        {
            public int Count;
        }

        private struct IntEqualsPredicate : IWhereDelegado<int, int>
        {
            public ExecutionState State;
            public readonly int ExecutionCount => State?.Count ?? 0;
            public readonly bool Ejecutar(int item, int otro)
            {
                if (State != null) State.Count++;
                return item == otro;
            }
        }

        private struct IntDoubleSelector : ISelectDelegado<int, int>
        {
            public ExecutionState State;
            public readonly int ExecutionCount => State?.Count ?? 0;
            public readonly int Ejecutar(int item)
            {
                if (State != null) State.Count++;
                return item * 2;
            }
        }

        // Tier 1: Feature Coverage

        [Fact]
        public void ValueLINQDelayWhereDefersEvaluation()
        {
            int[] array = [1, 2, 3, 2, 4];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            IntEqualsPredicate predicate = new() { State = new ExecutionState() };

            // Act: Chain delay and where
            ValueLINQDelayStruct<int, ValueLINQWhereDelay<int, ValueLINQSessionEnumerator<int>, IntEqualsPredicate, int>> lazyPipeline = query.Delay().Where(2, ref predicate);

            // Assert: Predicate not executed yet
            _ = predicate.ExecutionCount.Should().Be(0);

            // Act: Enumerate
            int count = 0;
            foreach (ref readonly int item in lazyPipeline)
            {
                count++;
            }

            // Assert: Executed during enumeration
            _ = count.Should().Be(2);
            _ = predicate.ExecutionCount.Should().Be(5);
        }

        [Fact]
        public void ValueLINQDelaySelectDefersEvaluation()
        {
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            IntDoubleSelector selector = new() { State = new ExecutionState() };

            // Act: Chain delay and select
            ValueLINQDelayStruct<int, ValueLINQSelectDelay<int, ValueLINQSessionEnumerator<int>, IntDoubleSelector, int>> lazyPipeline = query.Delay().Select<int, IntDoubleSelector, int>(ref selector);

            // Assert: Selector not executed yet
            _ = selector.ExecutionCount.Should().Be(0);

            // Act: Enumerate
            int count = 0;
            foreach (ref readonly int item in lazyPipeline)
            {
                count++;
            }

            // Assert: Executed during enumeration
            _ = count.Should().Be(3);
            _ = selector.ExecutionCount.Should().Be(3);
        }

        [Fact]
        public void ValueLINQDelayChainedOperationsCorrectlyEvaluates()
        {
            int[] array = [1, 2, 3, 4, 5];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            IntEqualsPredicate predicate = new() { State = new ExecutionState() };
            IntDoubleSelector selector = new() { State = new ExecutionState() };

            ValueLINQDelayStruct<int, ValueLINQSelectDelay<int, ValueLINQWhereDelay<int, ValueLINQSessionEnumerator<int>, IntEqualsPredicate, int>, IntDoubleSelector, int>> lazyPipeline = query.Delay()
                                    .Where(3, ref predicate)
                                    .Select<int, IntDoubleSelector, int>(ref selector);

            int count = 0;
            int lastVal = 0;
            foreach (ref readonly int item in lazyPipeline)
            {
                count++;
                lastVal = item;
            }

            _ = count.Should().Be(1);
            _ = lastVal.Should().Be(6);
        }

        [Fact]
        public void ValueLINQDelayAllowsRefStructConstraintDoesNotBox()
        {
            // Verify that we can pass a custom ref struct and it executes with zero heap allocations.
            int[] array = [1, 2, 3];

            // Correctness check
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();
                ProcessRefStructPipeline(pipeline);
            }

            // Allocation check
            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();
                int sum = 0;
                foreach (ref readonly int item in pipeline)
                {
                    sum += item;
                }
            });
        }

        private static void ProcessRefStructPipeline<TEnumerator>(ValueLINQDelayStruct<int, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<int>, allows ref struct
        {
            int sum = 0;
            foreach (ref readonly int item in pipeline)
            {
                sum += item;
            }
            _ = sum.Should().Be(6);
        }

        [Fact]
        public void ValueLINQDelayMultiTargetingConditionalCompilation() =>
            // Under net9.0, Delay is compiled. Under net8.0, it is not.
            // This test is conditionally compiled via the file-level define.
            _ = typeof(ValueLINQExtensions).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Should().Contain(m => m.Name == "Delay");

        // Tier 2: Boundary & Corner

        [Fact]
        public void ValueLINQDelayEmptySourceReturnsEmptyEnumeration()
        {
            int[] array = [];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

            int count = 0;
            foreach (ref readonly int item in pipeline)
            {
                count++;
            }

            _ = count.Should().Be(0);
        }

        [Fact]
        public void ValueLINQDelaySourceDisposedBeforeEnumerationThrowsException()
        {
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

            // Act: Dispose source query before enumeration
            query.Dispose();

            // Assert: Enumeration should throw session expired exception
            try
            {
                foreach (ref readonly int item in pipeline)
                {
                }
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException)
            {
                // Expected
            }
        }

        [Fact]
        public void ValueLINQDelayExceptionDuringEnumerationDisposesPipeline()
        {
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            IntEqualsPredicate predicate = new();

            ValueLINQDelayStruct<int, ValueLINQWhereDelay<int, ValueLINQSessionEnumerator<int>, IntEqualsPredicate, int>> pipeline = query.Delay().Where(2, ref predicate);

            try
            {
                foreach (ref readonly int item in pipeline)
                {
                    if (item == 2)
                        throw new InvalidOperationException("Simulated mid-enumeration error");
                }
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated mid-enumeration error")
            {
                // Expected
            }
        }

        [Fact]
        public void ValueLINQDelayMultipleEnumerationsBehaviorIsDeterministic()
        {
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

            int sum1 = 0;
            foreach (ref readonly int item in pipeline)
            {
                sum1 += item;
            }

            int sum2 = 0;
            foreach (ref readonly int item in pipeline)
            {
                sum2 += item;
            }

            _ = sum1.Should().Be(6);
            _ = sum2.Should().Be(6);
        }

        [Fact]
        public void ValueLINQDelayAllowsRefStructWithDeepCallStack()
        {
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

            CallStackDepth3(pipeline);
        }

        private static void CallStackDepth3<TEnumerator>(ValueLINQDelayStruct<int, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<int>, allows ref struct => CallStackDepth2(pipeline);

        private static void CallStackDepth2<TEnumerator>(ValueLINQDelayStruct<int, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<int>, allows ref struct => CallStackDepth1(pipeline);

        private static void CallStackDepth1<TEnumerator>(ValueLINQDelayStruct<int, TEnumerator> pipeline)
            where TEnumerator : IValueLINQEnumerator<int>, allows ref struct
        {
            int sum = 0;
            foreach (ref readonly int item in pipeline)
            {
                sum += item;
            }
            _ = sum.Should().Be(6);
        }

        // Tier 3: Cross-Feature Combinations (Pairwise Coverage)

        [Fact]
        public void CombinationC2LazyQueryWithGCTimeout()
        {
            ValueLINQStruct<int> query = array123.ToValueQuery();
            long token = query.Token;
            _ = query.Delay();

            // Simulate timeout by setting UltimoAcceso to an old timestamp
            ref MetadatosSesion<int> metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(token);
            metadatos.UltimoAcceso = 0; // ancient timestamp

            // Set cleanup time to zero
            System.Reflection.FieldInfo? field = typeof(ValueLINQStateManager<int>).GetField("_tiempoLimpieza", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            field!.SetValue(null, TimeSpan.Zero);

            // Act: Run GC cleanup
            System.Reflection.MethodInfo? cleanupMethod = typeof(ValueLINQGC).GetMethod("EjecutarLimpieza", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            _ = cleanupMethod!.Invoke(null, null);

            // Assert: The session should be collected and no longer valid
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void CombinationC3LazyQueryInsideSemaphore()
        {
            using SemaphoreSlim semaphore = new(1, 1);
            int[] array = [1, 2, 3];

            // Correctness check
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

                using SemaphoreLock semLock = semaphore.Esperar();

                int sum = 0;
                foreach (ref readonly int item in pipeline)
                {
                    sum += item;
                }
                _ = sum.Should().Be(6);
            }

            // Allocation check
            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

                using SemaphoreLock semLock = semaphore.Esperar();

                int sum = 0;
                foreach (ref readonly int item in pipeline)
                {
                    sum += item;
                }
            });
        }

        // Tier 4: Real-World Application Scenarios

        public struct SensorReading
        {
            internal int Id;
            internal double Value;
        }

        public struct CalibratedReading
        {
            internal int Id;
            internal double CalibratedValue;
        }

        public struct SensorNoisePredicate : IWhereDelegado<SensorReading, double>
        {
            public readonly bool Ejecutar(SensorReading item, double limit) => item.Value >= 0 && item.Value <= limit;
        }

        public struct Calibrator : ISelectDelegado<SensorReading, CalibratedReading>
        {
            public CalibratedReading Ejecutar(SensorReading item) => new() { Id = item.Id, CalibratedValue = item.Value * 1.05 };
        }

        [Fact]
        public void ScenarioSensorStreamProcessesLazilyWithoutBoxing()
        {
            SensorReading[] readings =
            [
                new() { Id = 1, Value = 10.0 },
                new() { Id = 2, Value = -5.0 }, // noise
                new() { Id = 3, Value = 50.0 },
                new() { Id = 4, Value = 150.0 }, // noise (limit is 100)
                new() { Id = 5, Value = 20.0 }
            ];

            CalibratedReading[] buffer = new CalibratedReading[5];

            // Correctness check
            {
                using ValueLINQStruct<SensorReading> query = readings.AsSpan().ToValueQuery();
                SensorNoisePredicate predicate = new();
                Calibrator calibrator = new();

                ValueLINQDelayStruct<CalibratedReading, ValueLINQSelectDelay<CalibratedReading, ValueLINQWhereDelay<SensorReading, ValueLINQSessionEnumerator<SensorReading>, SensorNoisePredicate, double>, Calibrator, SensorReading>> pipeline = query.Delay()
                                    .Where(100.0, ref predicate)
                                    .Select<SensorReading, Calibrator, CalibratedReading>(ref calibrator);

                int index = 0;
                foreach (ref readonly CalibratedReading item in pipeline)
                {
                    buffer[index++] = item;
                }

                _ = index.Should().Be(3);
                _ = buffer[0].Id.Should().Be(1);
                _ = buffer[0].CalibratedValue.Should().Be(10.5);
                _ = buffer[1].Id.Should().Be(3);
                _ = buffer[1].CalibratedValue.Should().Be(52.5);
                _ = buffer[2].Id.Should().Be(5);
                _ = buffer[2].CalibratedValue.Should().Be(21.0);
            }

            // Allocation check
            Array.Clear(buffer);
            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQStruct<SensorReading> query = readings.AsSpan().ToValueQuery();
                SensorNoisePredicate predicate = new();
                Calibrator calibrator = new();

                ValueLINQDelayStruct<CalibratedReading, ValueLINQSelectDelay<CalibratedReading, ValueLINQWhereDelay<SensorReading, ValueLINQSessionEnumerator<SensorReading>, SensorNoisePredicate, double>, Calibrator, SensorReading>> pipeline = query.Delay()
                                    .Where(100.0, ref predicate)
                                    .Select<SensorReading, Calibrator, CalibratedReading>(ref calibrator);

                int index = 0;
                foreach (ref readonly CalibratedReading item in pipeline)
                {
                    buffer[index++] = item;
                }
            });
        }
    }
}
#endif
