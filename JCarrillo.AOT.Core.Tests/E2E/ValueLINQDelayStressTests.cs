#if NET9_0_OR_GREATER
#pragma warning disable CA1707
using FluentAssertions;
using Xunit;
using System;
using System.Threading;
using System.Threading.Tasks;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ValueLINQDelayStressTests
    {
        private struct IntEqualsPredicate : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int otro) => item == otro;
        }

        private struct IntDoubleSelector : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        private static int GetActiveSlotsCount()
            => ValueLINQConfig.TamañoTabla - ValueLINQStateManager<int>.SlotsLibres;

        // ==========================================
        // TIER 1: MULTI-STAGE MOVENEXT() AFTER DISPOSAL
        // ==========================================

        [Fact]
        public void MoveNext_AfterExplicitDispose_SessionEnumerator_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            using var query = array.ToValueQuery();
            var pipeline = query.Delay();
            var enumerator = pipeline.GetEnumerator();

            // 1. MoveNext before dispose
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(1);

            // 2. Explicit Dispose
            enumerator.Dispose();

            // 3. Multi-stage MoveNext after dispose must return false
            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_BeforeFirstEnumerationDisposed_SessionEnumerator_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            using var query = array.ToValueQuery();
            var pipeline = query.Delay();
            var enumerator = pipeline.GetEnumerator();

            // 1. Dispose before ever calling MoveNext
            enumerator.Dispose();

            // 2. Multi-stage MoveNext after dispose must return false
            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_AfterAutoDisposal_SessionEnumerator_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            using var query = array.ToValueQuery();
            var pipeline = query.Delay();
            var enumerator = pipeline.GetEnumerator();

            // 1. Enumerate to completion
            enumerator.MoveNext().Should().BeTrue();
            enumerator.MoveNext().Should().BeTrue();
            enumerator.MoveNext().Should().BeTrue();
            enumerator.MoveNext().Should().BeFalse(); // Auto-disposal triggered here

            // 2. Multi-stage MoveNext after auto-disposal must return false
            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_AfterExplicitDispose_SourceEnumerator_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            var pipeline = array.ToValueDelayQuery();
            var enumerator = pipeline.GetEnumerator();

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Dispose();

            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_AfterExplicitDispose_ChunkDelay_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3, 4, 5];
            using var query = array.ToValueQuery();
            var pipeline = query.Delay().Chunk(2);
            var enumerator = pipeline.GetEnumerator();

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Dispose();

            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_AfterExplicitDispose_SelectDelay_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            using var query = array.ToValueQuery();
            var selector = new IntDoubleSelector();
            var pipeline = query.Delay().Select<IntDoubleSelector, int>(ref selector);
            var enumerator = pipeline.GetEnumerator();

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Dispose();

            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        [Fact]
        public void MoveNext_AfterExplicitDispose_WhereDelay_ConsistentlyReturnsFalse()
        {
            int[] array = [1, 2, 3];
            using var query = array.ToValueQuery();
            var predicate = new IntEqualsPredicate();
            var pipeline = query.Delay().Where(2, ref predicate);
            var enumerator = pipeline.GetEnumerator();

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Dispose();

            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext().Should().BeFalse();
            }
        }

        // ==========================================
        // TIER 2: NESTED & CONCURRENT ENUMERATION
        // ==========================================

        [Fact]
        public void NestedEnumeration_OnSameActiveSession_DoesNotLeakOrCorruptState()
        {
            int initialActive = GetActiveSlotsCount();

            int[] array = [1, 2, 3, 4, 5];
            using var query = array.ToValueQuery();
            var pipeline = query.Delay();

            int outerCount = 0;
            int innerCount = 0;

            bool lanzoElExterno = false;

            try
            {
                foreach (ref readonly int outerItem in pipeline)
                {
                    outerCount++;
                    // Nesting: starts another enumerator on the same session
                    foreach (ref readonly int innerItem in pipeline)
                    {
                        innerCount++;
                    }
                }
            }
            catch (ValueLinqSesionExpiradaException)
            {
                lanzoElExterno = true;
            }

            // El bucle interno agota y libera la sesión compartida, así que el externo la pierde tras su primera
            // iteración. Desde que perder la sesión lanza, el externo lo detecta en vez de terminar en silencio.
            outerCount.Should().Be(1);
            innerCount.Should().Be(5);
            lanzoElExterno.Should().BeTrue();

            // Verify that all slots are freed
            GetActiveSlotsCount().Should().Be(initialActive);
        }

        [Fact]
        public async Task ConcurrentEnumerationAndDisposal_OnSameSession_NoCorruptionOrLeak()
        {
            int initialActive = GetActiveSlotsCount();
            int[] array = [1, 2, 3, 4, 5];

            for (int iteration = 0; iteration < 50; iteration++)
            {
                using var query = array.ToValueQuery();
                const int numTasks = 10;
                Task[] tasks = new Task[numTasks];

                for (int i = 0; i < numTasks; i++)
                {
                    int taskId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            var localQuery = query; // copy struct with shared token
                            var pipeline = localQuery.Delay();
                            var enumerator = pipeline.GetEnumerator();

                            if (taskId % 2 == 0)
                            {
                                // Some threads dispose early
                                Thread.SpinWait(10);
                                enumerator.Dispose();
                            }
                            else
                            {
                                // Other threads enumerate
                                while (enumerator.MoveNext())
                                {
                                    _ = enumerator.Current;
                                    Thread.SpinWait(5);
                                }
                                enumerator.Dispose();
                            }
                        }
                        catch (ValueLinqSesionExpiradaException) { }
                        catch (ValueLinqTokenInvalidoException) { }
                    });
                }

                await Task.WhenAll(tasks);
            }

            // Verify no slots are leaked
            GetActiveSlotsCount().Should().Be(initialActive);
        }

        // ==========================================
        // TIER 3: IDEMPOTENCY / DOUBLE DISPOSE
        // ==========================================

        [Fact]
        public void Dispose_DoubleCall_IsIdempotent_OnAllEnumerators()
        {
            int initialActive = GetActiveSlotsCount();

            // 1. SessionEnumerator
            {
                int[] array = [1, 2, 3];
                using var query = array.ToValueQuery();
                var enumerator = query.Delay().GetEnumerator();
                
                try
                {
                    enumerator.Dispose();
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"SessionEnumerator Dispose threw: {ex.Message}");
                }
            }

            // 2. SourceEnumerator
            {
                int[] array = [1, 2, 3];
                var enumerator = array.ToValueDelayQuery().GetEnumerator();
                
                try
                {
                    enumerator.Dispose();
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"SourceEnumerator Dispose threw: {ex.Message}");
                }
            }

            // 3. ChunkDelay
            {
                int[] array = [1, 2, 3];
                using var query = array.ToValueQuery();
                var enumerator = query.Delay().Chunk(2).GetEnumerator();
                
                try
                {
                    enumerator.Dispose();
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"ChunkDelay Dispose threw: {ex.Message}");
                }
            }

            // 4. SelectDelay
            {
                int[] array = [1, 2, 3];
                using var query = array.ToValueQuery();
                var selector = new IntDoubleSelector();
                var enumerator = query.Delay().Select<IntDoubleSelector, int>(ref selector).GetEnumerator();
                
                try
                {
                    enumerator.Dispose();
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"SelectDelay Dispose threw: {ex.Message}");
                }
            }

            // 5. WhereDelay
            {
                int[] array = [1, 2, 3];
                using var query = array.ToValueQuery();
                var predicate = new IntEqualsPredicate();
                var enumerator = query.Delay().Where(2, ref predicate).GetEnumerator();
                
                try
                {
                    enumerator.Dispose();
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"WhereDelay Dispose threw: {ex.Message}");
                }
            }

            GetActiveSlotsCount().Should().Be(initialActive);
        }

        [Fact]
        public void Dispose_DefaultInitializedEnumerators_IsNoOpAndSafe()
        {
            // 1. SessionEnumerator
            {
                var enumerator = default(ValueLINQSessionEnumerator<int>);
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Default SessionEnumerator Dispose threw: {ex.Message}");
                }
            }

            // 2. SourceEnumerator
            {
                var enumerator = default(ValueLINQSourceEnumerator<int>);
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Default SourceEnumerator Dispose threw: {ex.Message}");
                }
            }

            // 3. ChunkDelay
            {
                var enumerator = default(ValueLINQChunkDelay<int, ValueLINQSessionEnumerator<int>>);
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Default ChunkDelay Dispose threw: {ex.Message}");
                }
            }

            // 4. SelectDelay
            {
                var enumerator = default(ValueLINQSelectDelay<int, ValueLINQSessionEnumerator<int>, IntDoubleSelector, int>);
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Default SelectDelay Dispose threw: {ex.Message}");
                }
            }

            // 5. WhereDelay
            {
                var enumerator = default(ValueLINQWhereDelay<int, ValueLINQSessionEnumerator<int>, IntEqualsPredicate, int>);
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Default WhereDelay Dispose threw: {ex.Message}");
                }
            }
        }

        // ==========================================
        // TIER 4: DOUBLE CONSUMPTION OF CHUNK PIPELINES
        // ==========================================

        [Fact]
        public void Chunk_DoubleConsumption_SecondEnumerationIsEmpty()
        {
            int[] array = [1, 2, 3, 4, 5, 6];
            var pipeline = array.ToValueDelayQuery().Chunk(2);

            int chunksPrimera = 0;
            foreach (var chunk in pipeline)
                chunksPrimera++;

            // La primera enumeración libera la sesión del buffer, así que la segunda ya no la encuentra. Desde que
            // perder la sesión lanza, reenumerar deja de entregar cero fragmentos en silencio.
            bool lanzoLaSegunda = false;
            int chunksSegunda = 0;

            try
            {
                foreach (var chunk in pipeline)
                    chunksSegunda++;
            }
            catch (ValueLinqSesionExpiradaException)
            {
                lanzoLaSegunda = true;
            }

            chunksPrimera.Should().Be(3);
            lanzoLaSegunda.Should().BeTrue();
            chunksSegunda.Should().Be(0);
        }

        [Fact]
        public void Chunk_DoubleConsumption_DoesNotDoubleReturnBufferToPool()
        {
            int[] array = [1, 2, 3, 4];
            var pipeline = array.ToValueDelayQuery().Chunk(2);

            foreach (var chunk in pipeline) { }

            // La segunda enumeración lanza al no encontrar la sesión; lo que se comprueba aquí es que aun así
            // el buffer no se devolvió dos veces al pool.
            try
            {
                foreach (var chunk in pipeline) { }
            }
            catch (ValueLinqSesionExpiradaException)
            {
            }

            int[] alquiler1 = System.Buffers.ArrayPool<int>.Shared.Rent(2);
            int[] alquiler2 = System.Buffers.ArrayPool<int>.Shared.Rent(2);
            try
            {
                ReferenceEquals(alquiler1, alquiler2).Should().BeFalse();
            }
            finally
            {
                System.Buffers.ArrayPool<int>.Shared.Return(alquiler1);
                System.Buffers.ArrayPool<int>.Shared.Return(alquiler2);
            }
        }

        [Fact]
        public void Chunk_WithReferenceTypes_ProducesCorrectChunksAndContent()
        {
            string[] array = ["a", "b", "c", "d", "e"];
            var pipeline = array.ToValueDelayQuery().Chunk(2);

            var recogidos = new List<string>();
            int chunks = 0;
            foreach (var chunk in pipeline)
            {
                chunks++;
                foreach (var elemento in chunk)
                    recogidos.Add(elemento);
            }

            chunks.Should().Be(3);
            recogidos.Should().Equal("a", "b", "c", "d", "e");
        }

        [Fact]
        public void Chunk_RepeatedFullConsumption_DoesNotLeakSessionSlots()
        {
            int[] array = [1, 2, 3, 4];
            int slotsAntes = GetActiveSlotsCount();

            for (int i = 0; i < ValueLINQConfig.TamañoTabla + 50; i++)
            {
                var pipeline = array.ToValueDelayQuery().Chunk(2);
                foreach (var chunk in pipeline) { }
            }

            GetActiveSlotsCount().Should().Be(slotsAntes);
        }
    }
}
#endif
