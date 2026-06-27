using System;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace JCarrillo.AOT.Core.Tests.Extensiones
{
    public class ValueLINQTests
    {
        #region Helpers y Delegados de Prueba

        private struct IntEqualsPredicate : IWhereDelegado<int, int>
        {
            public bool Ejecutar(int item, int otro) => item == otro;
        }

        private struct IntDoubleSelector : ISelectDelegado<int, int>
        {
            public int Ejecutar(int item) => item * 2;
        }

        private struct IntToStringSelector : ISelectDelegado<int, string>
        {
            public string Ejecutar(int item) => item.ToString();
        }

        private struct ThrowingPredicate : IWhereDelegado<int, int>
        {
            public bool Ejecutar(int item, int otro)
            {
                if (item == 2)
                    throw new InvalidOperationException("Simulated error");
                return true;
            }
        }

        private struct ThrowingSelector : ISelectDelegado<int, int>
        {
            public int Ejecutar(int item)
            {
                if (item == 2)
                    throw new InvalidOperationException("Simulated selector error");
                return item;
            }
        }

        private struct ChunkProcessorCounter : IProcesarChunkDelegado<int>
        {
            private readonly int[] _counter;
            public ChunkProcessorCounter(int[] counter) => _counter = counter;
            public void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                int count = 0;
                foreach (ref int item in listaChunk)
                    count++;
                _counter[0] += count;
            }
        }

        private struct ChunkProcessorThrowing : IProcesarChunkDelegado<int>
        {
            public void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                foreach (ref int item in listaChunk)
                    if (item == 3)
                        throw new InvalidOperationException("Simulated chunk processor error");
            }
        }

        private static int GetActiveSlotsCount<TItem>()
        {
            int topStack = (int)typeof(ValueLINQStateManager<TItem>)
                .GetField("_topStack", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(null)!;
            return 4096 - topStack;
        }

        #endregion

        #region 1. Pruebas de TokenHelper (Existentes)

        [Fact]
        public void TokenHelper_CrearToken_ShouldPackSlotAndVersionCorrectly()
        {
            int slot = 123;
            long version = 456789L;

            long token = TokenHelper.CrearToken(slot, version);

            TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void TokenHelper_VersionRightShift_ShouldPreventSignExtension()
        {
            int slot = 4095;
            long version = 0x7FFFFFFFFFFFL;

            long token = TokenHelper.CrearToken(slot, version);

            TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void TokenHelper_ReadWriteToken_ShouldBeCorrect()
        {
            long location = 0;
            long token = TokenHelper.CrearToken(1, 99);

            TokenHelper.EscribirToken(ref location, token);
            long read = TokenHelper.LeerToken(ref location);

            read.Should().Be(token);
        }

        #endregion

        #region 2. Pruebas de ValueLINQStateManager y Structs (Existentes y Nuevas)

        [Fact]
        public void ValueLINQStateManager_ShouldUseStackAllocatorAndVersionIncrement()
        {
            long token1;
            int index1;
            long version1;
            using (var struct1 = new ValueLINQStruct<int>(10))
            {
                token1 = struct1.Token;
                index1 = TokenHelper.ObtenerSlotIndex(token1);
                version1 = TokenHelper.ObtenerVersion(token1);
            }

            long token2;
            int index2;
            long version2;
            using (var struct2 = new ValueLINQStruct<int>(10))
            {
                token2 = struct2.Token;
                index2 = TokenHelper.ObtenerSlotIndex(token2);
                version2 = TokenHelper.ObtenerVersion(token2);
            }

            index2.Should().Be(index1, "it should reuse the released slot index from the O(1) stack allocator");
            version2.Should().Be(version1 + 1, "it should increment the slot version monotonically to prevent ABA");
        }

        [Fact]
        public void ValueLINQStateManager_ObtenerMetadatos_ShouldThrowSessionExpiredOnABAToken()
        {
            long token1;
            using (var struct1 = new ValueLINQStruct<int>(10))
                token1 = struct1.Token;

            using var struct2 = new ValueLINQStruct<int>(10);

            var struct1Fake = new ValueLINQStruct<int>(token1);
            try
            {
                struct1Fake.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQStruct_IsValido_ShouldReturnCorrectStatus()
        {
            ValueLINQStruct<int> query;

            using (query = new ValueLINQStruct<int>(10))
                query.IsValido.Should().BeTrue();

            query.IsValido.Should().BeFalse();
        }

        [Fact]
        public void ValueLINQStruct_Añadir_ShouldWorkCorrectly()
        {
            using var query = new ValueLINQStruct<int>(5);

            query.Añadir(10);
            query.Añadir(20);

            query.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQStruct_Dispose_DefaultStruct_ShouldNotCrash()
        {
            var query = new ValueLINQStruct<int>();

            Action act = () => query.Dispose();

            act.Should().NotThrow("Disposing a default struct should be a safe no-op and not crash the manager");
        }

        [Fact]
        public void ValueLINQRefStruct_Dispose_DefaultStruct_ShouldNotCrash()
        {
            var query = new ValueLINQRefStruct<int>();
            query.Dispose();
        }

        [Fact]
        public void ValueLINQStruct_Añadir_DefaultStruct_ShouldThrowValueLinqTokenInvalidoException()
        {
            var query = new ValueLINQStruct<int>();

            Action act = () => query.Añadir(42);
            act.Should().Throw<ValueLinqTokenInvalidoException>()
               .WithMessage("*no es válido*");
        }

        [Fact]
        public void ValueLINQRefStruct_Añadir_DefaultStruct_ShouldThrowValueLinqTokenInvalidoException()
        {
            var query = new ValueLINQRefStruct<int>();
            try
            {
                query.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqTokenInvalidoException");
            }
            catch (ValueLinqTokenInvalidoException ex)
            {
                ex.Message.Should().Contain("no es válido");
            }
        }

        [Fact]
        public void ValueLINQStruct_Operations_OnDefaultStruct_ShouldReturnValidEmptyQueryAndCleanUp()
        {
            var query = new ValueLINQStruct<int>();

            using var filtered = query.Where(2, new IntEqualsPredicate());
            using var projected = query.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
            using var chunks = query.Chunk(2);

            filtered.IsValido.Should().BeTrue();
            projected.IsValido.Should().BeTrue();
            chunks.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQRefStruct_Operations_OnDefaultStruct_ShouldReturnValidEmptyQueryAndCleanUp()
        {
            var query = new ValueLINQRefStruct<int>();

            using var filtered = query.Where(2, new IntEqualsPredicate());
            using var projected = query.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
            using var chunks = query.Chunk(2);

            filtered.IsValido.Should().BeTrue();
            projected.IsValido.Should().BeTrue();
            chunks.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQExtensions_SymmetricCreationAPI_ShouldReturnCorrectStructTypes()
        {
            var array = new int[] { 1, 2, 3 };
            var span = array.AsSpan(0, array.Length);
            var readOnlySpan = (ReadOnlySpan<int>)span;
            var memory = new Memory<int>(array);

            using (var pooledList = new PooledList<int>())
            {
                pooledList.AddRange(array.AsSpan(0, array.Length));

                // 1. Array
                using (ValueLINQStruct<int> qStruct = array.ToValueQuery())
                    qStruct.Token.Should().NotBe(0);

                using (ValueLINQRefStruct<int> qRef = array.ToValueRefQuery())
                    qRef.Token.Should().NotBe(0);

                // 2. Span
                using (ValueLINQStruct<int> qStruct = span.ToValueQuery())
                    qStruct.Token.Should().NotBe(0);

                using (ValueLINQRefStruct<int> qRef = span.ToValueRefQuery())
                    qRef.Token.Should().NotBe(0);

                // 3. ReadOnlySpan
                using (ValueLINQStruct<int> qStruct = readOnlySpan.ToValueQuery())
                    qStruct.Token.Should().NotBe(0);

                using (ValueLINQRefStruct<int> qRef = readOnlySpan.ToValueRefQuery())
                    qRef.Token.Should().NotBe(0);

                // 4. ref Memory
                var localMemory = memory;
                using (ValueLINQStruct<int> qStruct = localMemory.ToValueQuery())
                    qStruct.Token.Should().NotBe(0);

                using (ValueLINQRefStruct<int> qRef = localMemory.ToValueRefQuery())
                    qRef.Token.Should().NotBe(0);

                // 5. ref PooledList
                var localList = pooledList;
                using (ValueLINQStruct<int> qStruct = localList.ToValueQuery())
                    qStruct.Token.Should().NotBe(0);

                using (ValueLINQRefStruct<int> qRef = localList.ToValueRefQuery())
                    qRef.Token.Should().NotBe(0);
            }
        }

        #endregion

        #region 3. Pruebas de Comportamiento Lógico Exhaustivo (Existentes y Nuevas)

        [Fact]
        public void ValueLINQEnumerator_ShouldIterateCorrectlyByRef()
        {
            var array = new[] { 10, 20, 30 };
            using var query = array.ToValueQuery();

            int index = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(array[index]);
                item = item + 1;
                index++;
            }
            index.Should().Be(3);

            ref var metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(query.Token);
            metadatos.Array![0].Should().Be(11);
            metadatos.Array![1].Should().Be(21);
            metadatos.Array![2].Should().Be(31);
        }

        [Fact]
        public void ValueLINQExtensions_Where_ShouldFilterElementsAndCleanUp()
        {
            var array = new[] { 1, 2, 3, 2, 4 };
            var query = array.ToValueQuery();
            long originalToken = query.Token;

            var filtered = query.Where(2, new IntEqualsPredicate());

            filtered.IsValido.Should().BeTrue();
            ValueLINQStateManager<int>.IsMetadatoValido(originalToken).Should().BeFalse("Source should be disposed");
            
            int count = 0;
            foreach (ref int item in filtered)
            {
                item.Should().Be(2);
                count++;
            }
            count.Should().Be(2);
            filtered.Dispose();
        }

        [Fact]
        public void ValueLINQExtensions_Select_TypeProjection_ShouldWorkCorrectly()
        {
            var array = new[] { 1, 2, 3 };
            using var query = array.ToValueQuery();
            
            using var projected = query.Select<int, IntToStringSelector, string>(new IntToStringSelector());

            projected.IsValido.Should().BeTrue();
            
            int index = 0;
            foreach (ref string item in projected)
            {
                item.Should().Be(array[index].ToString());
                index++;
            }
            index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQExtensions_Where_EmptyCollection_ShouldProduceEmptyQuery()
        {
            var array = new int[0];
            using var query = array.ToValueQuery();
            using var filtered = query.Where(2, new IntEqualsPredicate());

            filtered.IsValido.Should().BeTrue();
            
            int count = 0;
            foreach (ref int item in filtered)
                count++;

            count.Should().Be(0);
        }

        [Fact]
        public void ValueLINQExtensions_Chunk_ShouldSplitElementsAndDisposeOrigin()
        {
            var array = new[] { 1, 2, 3, 4, 5 };
            var query = array.ToValueQuery();
            long originalToken = query.Token;

            var chunks = query.Chunk(2);

            chunks.IsValido.Should().BeTrue();
            ValueLINQStateManager<int>.IsMetadatoValido(originalToken).Should().BeFalse("Source should be disposed");

            int chunkIndex = 0;
            foreach (ref var chunk in chunks)
            {
                chunk.IsValido.Should().BeTrue();
                int elementCount = 0;
                foreach (ref int item in chunk)
                    elementCount++;

                if (chunkIndex < 2)
                    elementCount.Should().Be(2);
                else
                    elementCount.Should().Be(1);

                chunkIndex++;
            }
            chunkIndex.Should().Be(3);

            foreach (ref var chunk in chunks)
                chunk.Dispose();
            chunks.Dispose();
        }

        [Fact]
        public void ValueLINQExtensions_ProcessChunks_ShouldExecuteSuccessfully()
        {
            var array = new[] { 1, 2, 3, 4, 5 };
            var query = array.ToValueQuery();
            var chunks = query.Chunk(2);
            var sharedCounter = new int[1];
            var counter = new ChunkProcessorCounter(sharedCounter);

            chunks.ProcessChunks(counter);

            sharedCounter[0].Should().Be(5);
        }

        [Fact]
        public void ValueLINQExtensions_MaterializationOperators_ShouldCopyElementsAndDisposeSource()
        {
            var sourceArray = new[] { 10, 20, 30, 40 };

            // 1. ToList
            var query1 = sourceArray.ToValueQuery();
            long token1 = query1.Token;
            using (var list = query1.ToList())
            {
                list.Tamaño.Should().Be(4);
                list.Span[0].Should().Be(10);
                list.Span[3].Should().Be(40);
                ValueLINQStateManager<int>.IsMetadatoValido(token1).Should().BeFalse("ToList should dispose source session immediately");
            }

            // 2. ToArray
            var query2 = sourceArray.ToValueQuery();
            long token2 = query2.Token;
            using (var array = query2.ToArray())
            {
                array.Tamaño.Should().Be(4);
                array.Span[0].Should().Be(10);
                array.Span[3].Should().Be(40);
                ValueLINQStateManager<int>.IsMetadatoValido(token2).Should().BeFalse("ToArray should dispose source session immediately");
            }

            // 3. ToListRef
            var query3 = sourceArray.ToValueQuery();
            long token3 = query3.Token;
            using (var listRef = query3.ToListRef())
            {
                listRef.Tamaño.Should().Be(4);
                listRef.Span[0].Should().Be(10);
                listRef.Span[3].Should().Be(40);
                ValueLINQStateManager<int>.IsMetadatoValido(token3).Should().BeFalse("ToListRef should dispose source session immediately");
            }

            // 4. ToArrayRef
            var query4 = sourceArray.ToValueQuery();
            long token4 = query4.Token;
            using (var arrayRef = query4.ToArrayRef())
            {
                arrayRef.Tamaño.Should().Be(4);
                arrayRef.Span[0].Should().Be(10);
                arrayRef.Span[3].Should().Be(40);
                ValueLINQStateManager<int>.IsMetadatoValido(token4).Should().BeFalse("ToArrayRef should dispose source session immediately");
            }
        }

        [Fact]
        public void ValueLINQExtensions_MaterializationOperators_EmptyAndInvalidStreams_ShouldHandleGracefully()
        {
            var queryEmpty = new int[0].ToValueQuery();
            using (var list = queryEmpty.ToList())
                list.Tamaño.Should().Be(0);

            var queryEmptyArr = new int[0].ToValueQuery();
            using (var arr = queryEmptyArr.ToArray())
                arr.Tamaño.Should().Be(0);

            var queryInvalid = new ValueLINQRefStruct<int>();
            using (var list = queryInvalid.ToList())
                list.Tamaño.Should().Be(0);

            var queryInvalidArr = new ValueLINQRefStruct<int>();
            using (var arr = queryInvalidArr.ToArray())
                arr.Tamaño.Should().Be(0);
        }

        #endregion

        #region 4. Pruebas de Concurrencia, Carga y Limpieza Estática (Existentes y Nuevas)

        private struct UniqueTestType { }

        [Fact]
        public void ValueLINQStateManager_StaticConstructor_ShouldInitializeAllSlotsCorrectly()
        {
            int capacity = 4096;
            var tokens = new long[capacity];
            
            Action rentAll = () =>
            {
                for (int i = 0; i < capacity; i++)
                {
                    ref var metadatos = ref ValueLINQStateManager<UniqueTestType>.ObtenerMetadatos(10);
                    tokens[i] = metadatos.Token;
                }
            };
            
            rentAll.Should().NotThrow("All 4096 slots should be rentable because the stack is initialized exactly to 4096");
            
            Action rentOneMore = () => ValueLINQStateManager<UniqueTestType>.ObtenerMetadatos(10);
            rentOneMore.Should().Throw<InvalidOperationException>().WithMessage("*Capacidad máxima*");
            
            Action releaseAll = () =>
            {
                for (int i = 0; i < capacity; i++)
                    ValueLINQStateManager<UniqueTestType>.LiberarMetadatos(tokens[i]);
            };
            
            releaseAll.Should().NotThrow("All slots should be released cleanly");
            rentAll.Should().NotThrow("All slots should be rentable again without any stack corruption or overflow");
            
            releaseAll();
        }

        [Fact]
        public void ValueLINQStateManager_IsLimpiezaRequerida_ShouldReturnFalseForUninitializedOrDisposedSlots()
        {
            var type = typeof(ValueLINQStateManager<UniqueTestType>);
            var method = type.GetMethod("IsLimpiezaRequerida", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            method.Should().NotBeNull("IsLimpiezaRequerida method should exist");

            var result = method!.Invoke(null, new object[] { 0, System.Diagnostics.Stopwatch.GetTimestamp(), TimeSpan.FromMinutes(5) });
            result.Should().Be(false, "Uninitialized slot should not require cleanup");
        }

        [Fact]
        public void ValueLINQStateManager_UnderHighConcurrency_ShouldNotLeakOrCorruptStack()
        {
            int initialActive = GetActiveSlotsCount<int>();

            int iterations = 1000;
            int degreeOfParallelism = 10;

            Parallel.For(0, iterations, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism }, i =>
            {
                var localArray = new[] { i, i + 1, i + 2, i + 3 };
                using var query = localArray.ToValueQuery();
                using var filtered = query.Where(i + 1, new IntEqualsPredicate());
                using var projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
                using var resultList = projected.ToArray();

                resultList.Tamaño.Should().BeInRange(0, 4);
            });

            int finalActive = GetActiveSlotsCount<int>();
            finalActive.Should().Be(initialActive, "All slots must be cleanly returned to the allocator stack after concurrent executions");
        }

        #endregion

        #region 5. Pruebas de Programación Defensiva y Recuperación ante Excepciones (Nuevas)

        [Fact]
        public void ValueLINQExtensions_Where_ShouldDisposeBothOnException()
        {
            var array = new[] { 1, 2, 3 };

            for (int i = 0; i < 5000; i++)
            {
                var query = array.ToValueQuery();
                try
                {
                    query.Where(0, new ThrowingPredicate());
                    Assert.Fail("Should have thrown InvalidOperationException");
                }
                catch (InvalidOperationException ex) when (ex.Message == "Simulated error")
                {
                    // Expected
                }
            }
        }

        [Fact]
        public void ValueLINQExtensions_Chunk_ShouldPreventLeaksOnException()
        {
            var array = new[] { 1, 2, 3, 4, 5 };

            for (int i = 0; i < 2000; i++)
            {
                var query = array.ToValueQuery();
                var chunks = query.Chunk(2);
                bool isExcepcionLanzada = false;
                try
                {
                    chunks.ProcessChunks(new ChunkProcessorThrowing());
                }
                catch (InvalidOperationException ex) when (ex.Message == "Simulated chunk processor error")
                {
                    isExcepcionLanzada = true;
                }

                if (!isExcepcionLanzada)
                    throw new Exception("Simulated chunk processor error was not thrown!");
            }
        }

        [Fact]
        public void Where_ShouldReclaimBuffers_WhenPredicateThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            var array = new[] { 1, 2, 3 };
            var query = array.ToValueQuery();

            try
            {
                query.Where(0, new ThrowingPredicate());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated error")
            {
                // Expected
            }
            
            GetActiveSlotsCount<int>().Should().Be(initialActive, "active slot count must return to initial state to prevent memory leaks");
        }

        [Fact]
        public void Select_ShouldReclaimBuffers_WhenSelectorThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            var array = new[] { 1, 2, 3 };
            var query = array.ToValueQuery();

            try
            {
                query.Select<int, ThrowingSelector, int>(new ThrowingSelector());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated selector error")
            {
                // Expected
            }
            
            GetActiveSlotsCount<int>().Should().Be(initialActive, "active slot count must return to initial state to prevent memory leaks");
        }

        [Fact]
        public void ProcessChunks_ShouldReclaimBuffers_WhenProcessorThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            var array = new[] { 1, 2, 3, 4, 5 };
            var query = array.ToValueQuery();
            var chunks = query.Chunk(2);

            try
            {
                chunks.ProcessChunks(new ChunkProcessorThrowing());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated chunk processor error")
            {
                // Expected
            }
            
            GetActiveSlotsCount<int>().Should().Be(initialActive, "all rented chunk buffers must be immediately returned to the pool");
        }

        #endregion

        #region 6. Pruebas de Rendimiento y Cero Asignaciones (Nuevas)

        [Fact]
        public void ValueLINQ_QueryPipeline_ShouldHaveZeroHeapAllocations()
        {
            var array = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            
            // Warmup para compilar por JIT todos los métodos involucrados
            {
                using var query = array.ToValueQuery();
                using var filtered = query.Where(2, new IntEqualsPredicate());
                using var projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
                using var chunks = projected.Chunk(2);
                chunks.ProcessChunks(new ChunkProcessorCounter(new int[1]));
            }

            var counterArray = new int[1];

            // Registro de memoria asignada antes del Act
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            
            using (var query = array.ToValueQuery())
            using (var filtered = query.Where(2, new IntEqualsPredicate()))
            using (var projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector()))
            using (var chunks = projected.Chunk(2))
                chunks.ProcessChunks(new ChunkProcessorCounter(counterArray));
            
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocated = bytesAfter - bytesBefore;
            
            allocated.Should().Be(0, "the entire querying pipeline must run with exactly zero heap allocations");
        }

        #endregion

        #region 7. Pruebas de Añadir(ReadOnlySpan<T> span) (Nuevas)

        [Fact]
        public void ValueLINQStruct_AñadirReadOnlySpan_HappyCase_ShouldAddElementsCorrectly()
        {
            using var query = new ValueLINQStruct<int>(5);
            var elementsToAdd = new[] { 10, 20, 30 };

            query.Añadir(elementsToAdd.AsSpan());

            int index = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(elementsToAdd[index]);
                index++;
            }
            index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQRefStruct_AñadirReadOnlySpan_HappyCase_ShouldAddElementsCorrectly()
        {
            using var query = new ValueLINQRefStruct<int>(5);
            var elementsToAdd = new[] { 10, 20, 30 };

            query.Añadir(elementsToAdd.AsSpan());

            int index = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(elementsToAdd[index]);
                index++;
            }
            index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQStruct_AñadirReadOnlySpan_EmptySpan_ShouldBeSafeNoOp()
        {
            using var query = new ValueLINQStruct<int>(5);
            query.Añadir(10);

            query.Añadir(ReadOnlySpan<int>.Empty);

            int count = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(10);
                count++;
            }
            count.Should().Be(1);
        }

        [Fact]
        public void ValueLINQRefStruct_AñadirReadOnlySpan_EmptySpan_ShouldBeSafeNoOp()
        {
            using var query = new ValueLINQRefStruct<int>(5);
            query.Añadir(10);

            query.Añadir(ReadOnlySpan<int>.Empty);

            int count = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(10);
                count++;
            }
            count.Should().Be(1);
        }

        [Fact]
        public void ValueLINQStruct_AñadirReadOnlySpan_Resize_ShouldResizeCorrectlyAndKeepIntegrity()
        {
            using var query = new ValueLINQStruct<int>(2);
            query.Añadir(1);
            query.Añadir(2);

            var elementsToAdd = new[] { 3, 4, 5, 6, 7, 8 };
            query.Añadir(elementsToAdd.AsSpan());

            var expected = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            int index = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(expected[index]);
                index++;
            }
            index.Should().Be(8);
        }

        [Fact]
        public void ValueLINQRefStruct_AñadirReadOnlySpan_Resize_ShouldResizeCorrectlyAndKeepIntegrity()
        {
            using var query = new ValueLINQRefStruct<int>(2);
            query.Añadir(1);
            query.Añadir(2);

            var elementsToAdd = new[] { 3, 4, 5, 6, 7, 8 };
            query.Añadir(elementsToAdd.AsSpan());

            var expected = new[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            int index = 0;
            foreach (ref int item in query)
            {
                item.Should().Be(expected[index]);
                index++;
            }
            index.Should().Be(8);
        }

        [Fact]
        public void ValueLINQStruct_AñadirReadOnlySpan_AfterDispose_ShouldThrowValueLinqSesionExpiradaException()
        {
            var query = new ValueLINQStruct<int>(5);
            query.Añadir(10);
            query.Dispose();

            var elementsToAdd = new[] { 20, 30 };
            try
            {
                query.Añadir(elementsToAdd.AsSpan());
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQRefStruct_AñadirReadOnlySpan_AfterDispose_ShouldThrowValueLinqSesionExpiradaException()
        {
            var query = new ValueLINQRefStruct<int>(5);
            query.Añadir(10);
            query.Dispose();

            var elementsToAdd = new[] { 20, 30 };
            try
            {
                query.Añadir(elementsToAdd.AsSpan());
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                ex.Message.Should().Contain("expirado");
            }
        }

        #endregion
    }
}
