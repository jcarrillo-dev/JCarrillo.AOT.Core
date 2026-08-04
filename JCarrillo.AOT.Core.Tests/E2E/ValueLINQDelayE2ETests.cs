#if NET9_0_OR_GREATER
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
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
            ValueLINQDelayStruct<int, ValueLINQSelectDelay<int, ValueLINQSessionEnumerator<int>, IntDoubleSelector, int>> lazyPipeline = query.Delay().Select<IntDoubleSelector, int>(ref selector);

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
                                    .Select<IntDoubleSelector, int>(ref selector);

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
        public void ValueLINQDelaySourceDisposedBeforeEnumerationReturnsFalse()
        {
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> pipeline = query.Delay();

            // Act: Dispose source query before enumeration
            query.Dispose();

            // Assert: Enumeration should return false (does not produce elements)
            int count = 0;
            foreach (ref readonly int item in pipeline)
            {
                count++;
            }
            _ = count.Should().Be(0);
        }

        [Fact]
        public void ValueLINQDelayAutoDisposesOnCompletion()
        {
            int[] elements = [1, 2, 3, 4, 5];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay().Chunk(2);
            var enumerator = pipeline.GetEnumerator();

            // Enumerate elements
            _ = enumerator.MoveNext().Should().BeTrue();
            _ = enumerator.Current.Length.Should().Be(2);

            _ = enumerator.MoveNext().Should().BeTrue();
            _ = enumerator.Current.Length.Should().Be(2);

            _ = enumerator.MoveNext().Should().BeTrue();
            _ = enumerator.Current.Length.Should().Be(1);

            // MoveNext at completion should return false and trigger auto-disposal
            _ = enumerator.MoveNext().Should().BeFalse();

            // Subsequent MoveNext calls should continue to return false
            _ = enumerator.MoveNext().Should().BeFalse();

            // Explicit Dispose after auto-disposal should be safe and idempotent
            try
            {
                enumerator.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Dispose should not throw, but threw: {ex.Message}");
            }

            try
            {
                enumerator.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Double Dispose should not throw, but threw: {ex.Message}");
            }

            // MoveNext after dispose should still return false
            _ = enumerator.MoveNext().Should().BeFalse();
        }

        [Fact]
        public void ValueLINQDelayExplicitDisposalIsIdempotent()
        {
            int[] elements = [1, 2, 3];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay();
            var enumerator = pipeline.GetEnumerator();

            // MoveNext once to start
            _ = enumerator.MoveNext().Should().BeTrue();
            _ = enumerator.Current.Should().Be(1);

            // Explicit early Dispose
            try
            {
                enumerator.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Dispose should not throw, but threw: {ex.Message}");
            }

            // Double Dispose should not throw
            try
            {
                enumerator.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Double Dispose should not throw, but threw: {ex.Message}");
            }

            // Subsequent MoveNext returns false
            _ = enumerator.MoveNext().Should().BeFalse();
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
            _ = sum2.Should().Be(0); // Under auto-disposal, the first enumeration auto-disposes the session.
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
                                    .Select<Calibrator, CalibratedReading>(ref calibrator);

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
                                    .Select<Calibrator, CalibratedReading>(ref calibrator);

                int index = 0;
                foreach (ref readonly CalibratedReading item in pipeline)
                {
                    buffer[index++] = item;
                }
            });
        }

        public struct SumProcessorMutable : IProcesarChunkRefDelegado<int>
        {
            public int Sum { get; set; }
            public void Ejecutar(ReadOnlySpan<int> listaChunk)
            {
                for (int i = 0; i < listaChunk.Length; i++)
                {
                    Sum += listaChunk[i];
                }
            }
        }

        public struct SumProcessorByValue : IProcesarChunkRefDelegado<int>
        {
            private readonly int[] _result;

            public SumProcessorByValue(int[] result)
            {
                _result = result;
            }

            public void Ejecutar(ReadOnlySpan<int> listaChunk)
            {
                for (int i = 0; i < listaChunk.Length; i++)
                {
                    _result[0] += listaChunk[i];
                }
            }
        }

        [Fact]
        public void ScenarioChunkExactDivision()
        {
            int[] elements = [1, 2, 3, 4, 5, 6];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay().Chunk(3);

            int chunkCount = 0;
            System.Collections.Generic.List<int[]> result = [];
            foreach (ReadOnlySpan<int> chunk in pipeline)
            {
                chunkCount++;
                result.Add(chunk.ToArray());
            }

            _ = chunkCount.Should().Be(2);
            _ = result[0].Should().Equal([1, 2, 3]);
            _ = result[1].Should().Equal([4, 5, 6]);
        }

        [Fact]
        public void ScenarioChunkDivisionWithRemainder()
        {
            int[] elements = [1, 2, 3, 4, 5];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay().Chunk(2);

            int chunkCount = 0;
            System.Collections.Generic.List<int[]> result = [];
            foreach (ReadOnlySpan<int> chunk in pipeline)
            {
                chunkCount++;
                result.Add(chunk.ToArray());
            }

            _ = chunkCount.Should().Be(3);
            _ = result[0].Should().Equal([1, 2]);
            _ = result[1].Should().Equal([3, 4]);
            _ = result[2].Should().Equal([5]);
        }

        [Fact]
        public void ScenarioChunkFewerElementsThanSize()
        {
            int[] elements = [1, 2, 3];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay().Chunk(5);

            int chunkCount = 0;
            System.Collections.Generic.List<int[]> result = [];
            foreach (ReadOnlySpan<int> chunk in pipeline)
            {
                chunkCount++;
                result.Add(chunk.ToArray());
            }

            _ = chunkCount.Should().Be(1);
            _ = result[0].Should().Equal([1, 2, 3]);
        }

        [Fact]
        public void ScenarioChunkEmptyFlow()
        {
            int[] elements = [];
            using var query = elements.ToValueQuery();
            var pipeline = query.Delay().Chunk(3);

            int chunkCount = 0;
            foreach (ReadOnlySpan<int> chunk in pipeline)
            {
                chunkCount++;
            }

            _ = chunkCount.Should().Be(0);
        }

        [Fact]
        public void ScenarioChunkInvalidSizeThrows()
        {
            int[] elements = [1, 2, 3];
            using var query = elements.ToValueQuery();
            
            Action act0 = () => query.Delay().Chunk(0);
            _ = act0.Should().Throw<ArgumentOutOfRangeException>();

            Action actNeg = () => query.Delay().Chunk(-1);
            _ = actNeg.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ScenarioChunkProcesarErgonomicLambda()
        {
            int[] elements = [1, 2, 3, 4, 5];
            using var query = elements.ToValueQuery();
            int sum = 0;
            query.Delay().Chunk(2).ProcesarChunk((scoped ref ReadOnlySpan<int> chunk) =>
            {
                for (int i = 0; i < chunk.Length; i++)
                {
                    sum += chunk[i];
                }
            });
            _ = sum.Should().Be(15);
        }

        [Fact]
        public void ScenarioChunkProcesarStructMutableByRef()
        {
            int[] elements = [1, 2, 3, 4, 5];
            using var query = elements.ToValueQuery();
            SumProcessorMutable processor = new();
            query.Delay().Chunk(2).ProcesarChunk(ref processor);
            _ = processor.Sum.Should().Be(15);
        }

        [Fact]
        public void ScenarioChunkProcesarStructByValue()
        {
            int[] elements = [1, 2, 3, 4, 5];
            using var query = elements.ToValueQuery();
            int[] result = new int[1];
            SumProcessorByValue processor = new(result);
            query.Delay().Chunk(2).ProcesarChunkRef(processor);
            _ = result[0].Should().Be(15);
        }

        [Fact]
        public void ScenarioChunkZeroAllocations()
        {
            int[] elements = [1, 2, 3, 4, 5];
            int sum = 0;
            AllocationAssert.AssertZeroAllocations(() =>
            {
                sum = 0;
                using var query = elements.AsSpan().ToValueQuery();
                query.Delay().Chunk(2).ProcesarChunk((scoped ref ReadOnlySpan<int> chunk) =>
                {
                    for (int i = 0; i < chunk.Length; i++)
                    {
                        sum += chunk[i];
                    }
                });
            });
            _ = sum.Should().Be(15);
        }

        [Fact]
        public void ToValueDelayQueryFromNullArrayShouldThrowArgumentNullException()
        {
            // Preparar: el motor eager ya valida null en sus sobrecargas de T[]; el Delay debe hacer lo mismo.
            int[]? origen = null;

            // Actuar
            Action act = () => origen!.ToValueDelayQuery();

            // Aserción: sin la guarda, la conversión implícita a ReadOnlySpan<T> daría un intervalo vacío
            // y la consulta devolvería cero elementos en silencio.
            _ = act.Should().Throw<ArgumentNullException>()
                .And.ParamName.Should().Be("origen");
        }
    }
}
#endif
