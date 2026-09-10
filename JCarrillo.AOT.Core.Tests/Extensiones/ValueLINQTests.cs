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
            public readonly bool Ejecutar(int item, int otro) => item == otro;
        }

        private struct IntDoubleSelector : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        private struct IntToStringSelector : ISelectDelegado<int, string>
        {
            public readonly string Ejecutar(int item) => item.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private struct ThrowingPredicate : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int otro) => item == 2 ? throw new InvalidOperationException("Simulated error") : true;
        }

        private struct ThrowingSelector : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item == 2 ? throw new InvalidOperationException("Simulated selector error") : item;
        }

        private readonly struct ChunkProcessorCounter(int[] counter) : IProcesarChunkDelegado<int>
        {
            private readonly int[] _counter = counter;

            public readonly void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                int count = 0;
                foreach (ref int item in listaChunk)
                    count++;
                _counter[0] += count;
            }
        }

        private struct ChunkProcessorThrowing : IProcesarChunkDelegado<int>
        {
            public readonly void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                foreach (ref int item in listaChunk)
                    if (item == 3)
                        throw new InvalidOperationException("Simulated chunk processor error");
            }
        }

        private static int GetActiveSlotsCount<TItem>()
            => ValueLINQConfig.TamañoTabla - ValueLINQStateManager<TItem>.SlotsLibres;

        #endregion

        #region 1. Pruebas de TokenHelper (Existentes)

        [Fact]
        public void TokenHelperCrearTokenShouldPackSlotAndVersionCorrectly()
        {
            int slot = 123;
            long version = 456789L;

            long token = TokenHelper.CrearToken(slot, version);

            _ = TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            _ = TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void TokenHelperVersionRightShiftShouldPreventSignExtension()
        {
            int slot = 4095;
            long version = (1L << ValueLINQConfig.VersionBits) - 1;

            long token = TokenHelper.CrearToken(slot, version);

            _ = token.Should().BeNegative();
            _ = TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            _ = TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void TokenHelperCrearTokenShouldPackArenaYGeneracionCorrectly()
        {
            int slot = 4095;
            int arenaId = 4095;
            long arenaGen = ValueLINQConfig.ArenaGenMask;
            long version = (1L << ValueLINQConfig.VersionBits) - 1;

            long token = TokenHelper.CrearToken(slot, arenaId, arenaGen, version);

            _ = TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            _ = TokenHelper.ObtenerArenaId(token).Should().Be(arenaId);
            _ = TokenHelper.ObtenerArenaGen(token).Should().Be((int)arenaGen);
            _ = TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void TokenHelperCrearTokenFieldsShouldNotContaminateEachOther()
        {
            long tokenSlotLleno = TokenHelper.CrearToken(4095, 0, 0L);
            long tokenArenaLlena = TokenHelper.CrearToken(0, 4095, 0L);

            _ = TokenHelper.ObtenerArenaId(tokenSlotLleno).Should().Be(0);
            _ = TokenHelper.ObtenerVersion(tokenSlotLleno).Should().Be(0);
            _ = TokenHelper.ObtenerSlotIndex(tokenArenaLlena).Should().Be(0);
            _ = TokenHelper.ObtenerVersion(tokenArenaLlena).Should().Be(0);
        }

        [Fact]
        public void TokenHelperCrearTokenTwoArgsShouldDelegateToArenaZero()
        {
            long token = TokenHelper.CrearToken(7, 42L);

            _ = TokenHelper.ObtenerArenaId(token).Should().Be(0);
            _ = token.Should().Be(TokenHelper.CrearToken(7, 0, 42L));
        }

        [Fact]
        public void TokenHelperReadWriteTokenShouldBeCorrect()
        {
            long location = 0;
            long token = TokenHelper.CrearToken(1, 99);

            TokenHelper.EscribirToken(ref location, token);
            long read = TokenHelper.LeerToken(ref location);

            _ = read.Should().Be(token);
        }

        #endregion

        #region 2. Pruebas de ValueLINQStateManager y Structs (Existentes y Nuevas)

        [Fact]
        public void ValueLINQStateManagerShouldUseStackAllocatorAndVersionIncrement()
        {
            long token1;
            int index1;
            long version1;
            using (ValueLINQStruct<int> struct1 = new(10))
            {
                token1 = struct1.Token;
                index1 = TokenHelper.ObtenerSlotIndex(token1);
                version1 = TokenHelper.ObtenerVersion(token1);
            }

            long token2;
            int index2;
            long version2;
            using (ValueLINQStruct<int> struct2 = new(10))
            {
                token2 = struct2.Token;
                index2 = TokenHelper.ObtenerSlotIndex(token2);
                version2 = TokenHelper.ObtenerVersion(token2);
            }

            _ = index2.Should().Be(index1, "it should reuse the released slot index from the O(1) stack allocator");
            _ = version2.Should().Be(version1 + 1, "it should increment the slot version monotonically to prevent ABA");
        }

        [Fact]
        public void ValueLINQStateManagerObtenerMetadatosShouldThrowSessionExpiredOnABAToken()
        {
            long token1;
            using (ValueLINQStruct<int> struct1 = new(10))
                token1 = struct1.Token;

            using ValueLINQStruct<int> struct2 = new(10);

            ValueLINQStruct<int> struct1Fake = new(token1);
            try
            {
                struct1Fake.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                _ = ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQStructIsValidoShouldReturnCorrectStatus()
        {
            ValueLINQStruct<int> query;

            using (query = new ValueLINQStruct<int>(10))
                _ = query.IsValido.Should().BeTrue();

            _ = query.IsValido.Should().BeFalse();
        }

        [Fact]
        public void ValueLINQStructAñadirShouldWorkCorrectly()
        {
            using ValueLINQStruct<int> query = new(5);

            query.Añadir(10);
            query.Añadir(20);

            _ = query.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQStructDisposeDefaultStructShouldNotCrash()
        {
            ValueLINQStruct<int> query = new();

            Action act = query.Dispose;

            _ = act.Should().NotThrow("Disposing a default struct should be a safe no-op and not crash the manager");
        }

        [Fact]
        public void ValueLINQRefStructDisposeDefaultStructShouldNotCrash()
        {
            ValueLINQRefStruct<int> query = new();
            query.Dispose();
        }

        [Fact]
        public void ValueLINQStructAñadirDefaultStructShouldThrowValueLinqTokenInvalidoException()
        {
            ValueLINQStruct<int> query = new();

            Action act = () => query.Añadir(42);
            _ = act.Should().Throw<ValueLinqTokenInvalidoException>()
               .WithMessage("*no es válido*");
        }

        [Fact]
        public void ValueLINQRefStructAñadirDefaultStructShouldThrowValueLinqTokenInvalidoException()
        {
            ValueLINQRefStruct<int> query = new();
            try
            {
                query.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqTokenInvalidoException");
            }
            catch (ValueLinqTokenInvalidoException ex)
            {
                _ = ex.Message.Should().Contain("no es válido");
            }
        }

        [Fact]
        public void ValueLINQStructOperationsOnDefaultStructShouldReturnValidEmptyQueryAndCleanUp()
        {
            ValueLINQStruct<int> query = new();

            using ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate());
            using ValueLINQStruct<int> projected = query.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            _ = filtered.IsValido.Should().BeTrue();
            _ = projected.IsValido.Should().BeTrue();
            _ = chunks.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQRefStructOperationsOnDefaultStructShouldReturnValidEmptyQueryAndCleanUp()
        {
            ValueLINQRefStruct<int> query = new();

            using ValueLINQRefStruct<int> filtered = query.Where(2, new IntEqualsPredicate());
            using ValueLINQRefStruct<int> projected = query.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            _ = filtered.IsValido.Should().BeTrue();
            _ = projected.IsValido.Should().BeTrue();
            _ = chunks.IsValido.Should().BeTrue();
        }

        [Fact]
        public void ValueLINQExtensionsSymmetricCreationAPIShouldReturnCorrectStructTypes()
        {
            int[] array = [1, 2, 3];
            Span<int> span = array.AsSpan(0, array.Length);
            ReadOnlySpan<int> readOnlySpan = span;
            Memory<int> memory = new(array);

            using PooledList<int> pooledList = new();
            pooledList.AddRange(array.AsSpan(0, array.Length));

            // 1. Array
            using (ValueLINQStruct<int> qStruct = array.ToValueQuery())
                _ = qStruct.Token.Should().NotBe(0);

            using (ValueLINQRefStruct<int> qRef = array.ToValueRefQuery())
                _ = qRef.Token.Should().NotBe(0);

            // 2. Span
            using (ValueLINQStruct<int> qStruct = span.ToValueQuery())
                _ = qStruct.Token.Should().NotBe(0);

            using (ValueLINQRefStruct<int> qRef = span.ToValueRefQuery())
                _ = qRef.Token.Should().NotBe(0);

            // 3. ReadOnlySpan
            using (ValueLINQStruct<int> qStruct = readOnlySpan.ToValueQuery())
                _ = qStruct.Token.Should().NotBe(0);

            using (ValueLINQRefStruct<int> qRef = readOnlySpan.ToValueRefQuery())
                _ = qRef.Token.Should().NotBe(0);

            // 4. ref Memory
            Memory<int> localMemory = memory;
            using (ValueLINQStruct<int> qStruct = localMemory.ToValueQuery())
                _ = qStruct.Token.Should().NotBe(0);

            using (ValueLINQRefStruct<int> qRef = localMemory.ToValueRefQuery())
                _ = qRef.Token.Should().NotBe(0);

            // 5. ref PooledList
            PooledList<int> localList = pooledList;
            using (ValueLINQStruct<int> qStruct = localList.ToValueQuery())
                _ = qStruct.Token.Should().NotBe(0);

            using (ValueLINQRefStruct<int> qRef = localList.ToValueRefQuery())
                _ = qRef.Token.Should().NotBe(0);
        }

        #endregion

        #region 3. Pruebas de Comportamiento Lógico Exhaustivo (Existentes y Nuevas)

        [Fact]
        public void ValueLINQEnumeratorShouldIterateCorrectlyByRef()
        {
            int[] array = [10, 20, 30];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            int index = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(array[index]);
                item++;
                index++;
            }
            _ = index.Should().Be(3);

            ref MetadatosSesion<int> metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(query.Token);
            _ = metadatos.Array![0].Should().Be(11);
            _ = metadatos.Array![1].Should().Be(21);
            _ = metadatos.Array![2].Should().Be(31);
        }

        [Fact]
        public void ValueLINQExtensionsWhereShouldFilterElementsAndCleanUp()
        {
            int[] array = [1, 2, 3, 2, 4];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long originalToken = query.Token;

            ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate());

            _ = filtered.IsValido.Should().BeTrue();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(originalToken).Should().BeFalse("Source should be disposed");

            int count = 0;
            foreach (ref int item in filtered)
            {
                _ = item.Should().Be(2);
                count++;
            }
            _ = count.Should().Be(2);
            filtered.Dispose();
        }

        [Fact]
        public void ValueLINQExtensionsSelectTypeProjectionShouldWorkCorrectly()
        {
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            using ValueLINQStruct<string> projected = query.Select<int, IntToStringSelector, string>(new IntToStringSelector());

            _ = projected.IsValido.Should().BeTrue();

            int index = 0;
            foreach (ref string item in projected)
            {
                _ = item.Should().Be(array[index].ToString(System.Globalization.CultureInfo.InvariantCulture));
                index++;
            }
            _ = index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQExtensionsWhereEmptyCollectionShouldProduceEmptyQuery()
        {
            int[] array = [];
            using ValueLINQStruct<int> query = array.ToValueQuery();
            using ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate());

            _ = filtered.IsValido.Should().BeTrue();

            int count = 0;
            foreach (ref int item in filtered)
                count++;

            _ = count.Should().Be(0);
        }

        [Fact]
        public void ValueLINQExtensionsChunkShouldSplitElementsAndDisposeOrigin()
        {
            int[] array = [1, 2, 3, 4, 5];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long originalToken = query.Token;

            ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            _ = chunks.IsValido.Should().BeTrue();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(originalToken).Should().BeFalse("Source should be disposed");

            int chunkIndex = 0;
            foreach (ref ValueLINQStruct<int> chunk in chunks)
            {
                _ = chunk.IsValido.Should().BeTrue();
                int elementCount = 0;
                foreach (ref int item in chunk)
                    elementCount++;

                _ = elementCount.Should().Be(chunkIndex < 2 ? 2 : 1);

                chunkIndex++;
            }
            _ = chunkIndex.Should().Be(3);

            foreach (ref ValueLINQStruct<int> chunk in chunks)
                chunk.Dispose();
            chunks.Dispose();
        }

        [Fact]
        public void ValueLINQExtensionsProcesarChunksShouldExecuteSuccessfully()
        {
            int[] array = [1, 2, 3, 4, 5];
            ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);
            int[] sharedCounter = new int[1];
            ChunkProcessorCounter counter = new(sharedCounter);

            chunks.ProcesarChunks(counter);

            _ = sharedCounter[0].Should().Be(5);
        }

        [Fact]
        public void ValueLINQExtensionsMaterializationOperatorsShouldCopyElementsAndDisposeSource()
        {
            int[] sourceArray = [10, 20, 30, 40];

            // 1. ToList
            ValueLINQStruct<int> query1 = sourceArray.ToValueQuery();
            long token1 = query1.Token;
            using (PooledList<int> list = query1.ToList())
            {
                _ = list.Tamaño.Should().Be(4);
                _ = list.Span[0].Should().Be(10);
                _ = list.Span[3].Should().Be(40);
                _ = ValueLINQStateManager<int>.IsMetadatoValido(token1).Should().BeFalse("ToList should dispose source session immediately");
            }

            // 2. ToArray
            ValueLINQStruct<int> query2 = sourceArray.ToValueQuery();
            long token2 = query2.Token;
            using (PooledArray<int> array = query2.ToArray())
            {
                _ = array.Tamaño.Should().Be(4);
                _ = array.Span[0].Should().Be(10);
                _ = array.Span[3].Should().Be(40);
                _ = ValueLINQStateManager<int>.IsMetadatoValido(token2).Should().BeFalse("ToArray should dispose source session immediately");
            }

            // 3. ToListRef
            ValueLINQStruct<int> query3 = sourceArray.ToValueQuery();
            long token3 = query3.Token;
            using (PooledListRef<int> listRef = query3.ToListRef())
            {
                _ = listRef.Tamaño.Should().Be(4);
                _ = listRef.Span[0].Should().Be(10);
                _ = listRef.Span[3].Should().Be(40);
                _ = ValueLINQStateManager<int>.IsMetadatoValido(token3).Should().BeFalse("ToListRef should dispose source session immediately");
            }

            // 4. ToArrayRef
            ValueLINQStruct<int> query4 = sourceArray.ToValueQuery();
            long token4 = query4.Token;
            using PooledArrayRef<int> arrayRef = query4.ToArrayRef();
            _ = arrayRef.Tamaño.Should().Be(4);
            _ = arrayRef.Span[0].Should().Be(10);
            _ = arrayRef.Span[3].Should().Be(40);
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token4).Should().BeFalse("ToArrayRef should dispose source session immediately");
        }

        [Fact]
        public void ValueLINQExtensionsMaterializationOperatorsEmptyAndInvalidStreamsShouldHandleGracefully()
        {
            ValueLINQStruct<int> queryEmpty = Array.Empty<int>().ToValueQuery();
            using (PooledList<int> list = queryEmpty.ToList())
                _ = list.Tamaño.Should().Be(0);

            ValueLINQStruct<int> queryEmptyArr = Array.Empty<int>().ToValueQuery();
            using (PooledArray<int> arr = queryEmptyArr.ToArray())
                _ = arr.Tamaño.Should().Be(0);

            ValueLINQRefStruct<int> queryInvalid = new();
            using (PooledList<int> list = queryInvalid.ToList())
                _ = list.Tamaño.Should().Be(0);

            ValueLINQRefStruct<int> queryInvalidArr = new();
            using (PooledArray<int> arr = queryInvalidArr.ToArray())
                _ = arr.Tamaño.Should().Be(0);
        }

        #endregion

        #region 4. Pruebas de Concurrencia, Carga y Limpieza Estática (Existentes y Nuevas)

        private struct UniqueTestType { }

        [Fact]
        public void ValueLINQStateManagerShouldProvideFullCapacityAndRecycleSlots()
        {
            int capacity = 4096;
            long[] tokens = new long[capacity];

            Action rentAll = () =>
            {
                for (int i = 0; i < capacity; i++)
                {
                    ref MetadatosSesion<UniqueTestType> metadatos = ref ValueLINQStateManager<UniqueTestType>.ObtenerMetadatos(10);
                    tokens[i] = metadatos.Token;
                }
            };

            _ = rentAll.Should().NotThrow("All 4096 slots should be rentable because the stack is initialized exactly to 4096");

            Action rentOneMore = () => ValueLINQStateManager<UniqueTestType>.ObtenerMetadatos(10);
            _ = rentOneMore.Should().Throw<InvalidOperationException>().WithMessage("*Capacidad máxima*");

            Action releaseAll = () =>
            {
                for (int i = 0; i < capacity; i++)
                    ValueLINQStateManager<UniqueTestType>.LiberarMetadatos(tokens[i]);
            };

            _ = releaseAll.Should().NotThrow("All slots should be released cleanly");
            _ = rentAll.Should().NotThrow("All slots should be rentable again without any stack corruption or overflow");

            releaseAll();
        }

        [Fact]
        public void ValueLINQStateManagerLimpiezaIgnoraSlotsVirgenesYLiberados()
        {
            JCarrillo.AOT.Core.ValueLINQ.Arena.TablaSesiones<UniqueTestType> tabla = new(0);
            long viva = tabla.ObtenerMetadatos(2).Token;
            long liberado = tabla.ObtenerMetadatos(2).Token;
            tabla.LiberarMetadatos(liberado);
            int libresAntes = tabla.IndicesLibres;

            tabla.LimpiarSesionesExpiradas(ValueLINQConfig.TiempoLimpiezaMinimo);

            _ = tabla.IndicesLibres.Should().Be(libresAntes,
                "el barrido no debe empujar al stack slots vírgenes, ya liberados ni sesiones vivas no caducadas");
            _ = tabla.IsMetadatoValido(viva).Should().BeTrue();

            long tokenA = tabla.ObtenerMetadatos(2).Token;
            long tokenB = tabla.ObtenerMetadatos(2).Token;
            _ = TokenHelper.ObtenerSlotIndex(tokenA).Should().NotBe(TokenHelper.ObtenerSlotIndex(tokenB));
        }

        [Fact]
        public void ValueLINQStateManagerUnderHighConcurrencyShouldNotLeakOrCorruptStack()
        {
            int initialActive = GetActiveSlotsCount<int>();

            int iterations = 1000;
            int degreeOfParallelism = 10;

            _ = Parallel.For(0, iterations, new ParallelOptions { MaxDegreeOfParallelism = degreeOfParallelism }, i =>
            {
                int[] localArray = [i, i + 1, i + 2, i + 3];
                using ValueLINQStruct<int> query = localArray.ToValueQuery();
                using ValueLINQStruct<int> filtered = query.Where(i + 1, new IntEqualsPredicate());
                using ValueLINQStruct<int> projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
                using PooledArray<int> resultList = projected.ToArray();

                _ = resultList.Tamaño.Should().BeInRange(0, 4);
            });

            int finalActive = GetActiveSlotsCount<int>();
            _ = finalActive.Should().Be(initialActive, "All slots must be cleanly returned to the allocator stack after concurrent executions");
        }

        #endregion

        #region 5. Pruebas de Programación Defensiva y Recuperación ante Excepciones (Nuevas)

        [Fact]
        public void ValueLINQExtensionsWhereShouldDisposeBothOnException()
        {
            int[] array = [1, 2, 3];

            for (int i = 0; i < 5000; i++)
            {
                ValueLINQStruct<int> query = array.ToValueQuery();
                try
                {
                    _ = query.Where(0, new ThrowingPredicate());
                    Assert.Fail("Should have thrown InvalidOperationException");
                }
                catch (InvalidOperationException ex) when (ex.Message == "Simulated error")
                {
                    // Expected
                }
            }
        }

        [Fact]
        public void ValueLINQExtensionsChunkShouldPreventLeaksOnException()
        {
            int[] array = [1, 2, 3, 4, 5];

            for (int i = 0; i < 2000; i++)
            {
                ValueLINQStruct<int> query = array.ToValueQuery();
                ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);
                bool isExcepcionLanzada = false;
                try
                {
                    chunks.ProcesarChunks(new ChunkProcessorThrowing());
                }
                catch (InvalidOperationException ex) when (ex.Message == "Simulated chunk processor error")
                {
                    isExcepcionLanzada = true;
                }

                if (!isExcepcionLanzada)
                    throw new InvalidOperationException("Simulated chunk processor error was not thrown!");
            }
        }

        [Fact]
        public void WhereShouldReclaimBuffersWhenPredicateThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> query = array.ToValueQuery();

            try
            {
                _ = query.Where(0, new ThrowingPredicate());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated error")
            {
                // Expected
            }

            _ = GetActiveSlotsCount<int>().Should().Be(initialActive, "active slot count must return to initial state to prevent memory leaks");
        }

        [Fact]
        public void SelectShouldReclaimBuffersWhenSelectorThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> query = array.ToValueQuery();

            try
            {
                _ = query.Select<int, ThrowingSelector, int>(new ThrowingSelector());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated selector error")
            {
                // Expected
            }

            _ = GetActiveSlotsCount<int>().Should().Be(initialActive, "active slot count must return to initial state to prevent memory leaks");
        }

        [Fact]
        public void ProcesarChunksShouldReclaimBuffersWhenProcessorThrowsException()
        {
            int initialActive = GetActiveSlotsCount<int>();
            int[] array = [1, 2, 3, 4, 5];
            ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            try
            {
                chunks.ProcesarChunks(new ChunkProcessorThrowing());
                Assert.Fail("Should have thrown InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated chunk processor error")
            {
                // Expected
            }

            _ = GetActiveSlotsCount<int>().Should().Be(initialActive, "all rented chunk buffers must be immediately returned to the pool");
        }

        #endregion

        #region 6. Pruebas de Rendimiento y Cero Asignaciones (Nuevas)

        [Fact]
        public void ValueLINQQueryPipelineShouldHaveZeroHeapAllocations()
        {
            int[] array = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

            // Warmup para compilar por JIT todos los métodos involucrados
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                using ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate());
                using ValueLINQStruct<int> projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
                using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = projected.Chunk(2);
                chunks.ProcesarChunks(new ChunkProcessorCounter(new int[1]));
            }

            int[] counterArray = new int[1];

            // Registro de memoria asignada antes del Act
            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();

            using (ValueLINQStruct<int> query = array.ToValueQuery())
            using (ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate()))
            using (ValueLINQStruct<int> projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector()))
            using (ValueLINQRefStruct<ValueLINQStruct<int>> chunks = projected.Chunk(2))
                chunks.ProcesarChunks(new ChunkProcessorCounter(counterArray));

            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocated = bytesAfter - bytesBefore;

            _ = allocated.Should().Be(0, "the entire querying pipeline must run with exactly zero heap allocations");
        }

        #endregion

        #region 7. Pruebas de Añadir(ReadOnlySpan<T> span) (Nuevas)

        [Fact]
        public void ValueLINQStructAñadirReadOnlySpanHappyCaseShouldAddElementsCorrectly()
        {
            using ValueLINQStruct<int> query = new(5);
            int[] elementsToAdd = [10, 20, 30];

            query.Añadir(elementsToAdd.AsSpan());

            int index = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(elementsToAdd[index]);
                index++;
            }
            _ = index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQRefStructAñadirReadOnlySpanHappyCaseShouldAddElementsCorrectly()
        {
            using ValueLINQRefStruct<int> query = new(5);
            int[] elementsToAdd = [10, 20, 30];

            query.Añadir(elementsToAdd.AsSpan());

            int index = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(elementsToAdd[index]);
                index++;
            }
            _ = index.Should().Be(3);
        }

        [Fact]
        public void ValueLINQStructAñadirReadOnlySpanEmptySpanShouldBeSafeNoOp()
        {
            using ValueLINQStruct<int> query = new(5);
            query.Añadir(10);

            query.Añadir([]);

            int count = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(10);
                count++;
            }
            _ = count.Should().Be(1);
        }

        [Fact]
        public void ValueLINQRefStructAñadirReadOnlySpanEmptySpanShouldBeSafeNoOp()
        {
            using ValueLINQRefStruct<int> query = new(5);
            query.Añadir(10);

            query.Añadir([]);

            int count = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(10);
                count++;
            }
            _ = count.Should().Be(1);
        }

        [Fact]
        public void ValueLINQStructAñadirReadOnlySpanResizeShouldResizeCorrectlyAndKeepIntegrity()
        {
            using ValueLINQStruct<int> query = new(2);
            query.Añadir(1);
            query.Añadir(2);

            int[] elementsToAdd = [3, 4, 5, 6, 7, 8];
            query.Añadir(elementsToAdd.AsSpan());

            int[] expected = [1, 2, 3, 4, 5, 6, 7, 8];
            int index = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(expected[index]);
                index++;
            }
            _ = index.Should().Be(8);
        }

        [Fact]
        public void ValueLINQRefStructAñadirReadOnlySpanResizeShouldResizeCorrectlyAndKeepIntegrity()
        {
            using ValueLINQRefStruct<int> query = new(2);
            query.Añadir(1);
            query.Añadir(2);

            int[] elementsToAdd = [3, 4, 5, 6, 7, 8];
            query.Añadir(elementsToAdd.AsSpan());

            int[] expected = [1, 2, 3, 4, 5, 6, 7, 8];
            int index = 0;
            foreach (ref int item in query)
            {
                _ = item.Should().Be(expected[index]);
                index++;
            }
            _ = index.Should().Be(8);
        }

        [Fact]
        public void ValueLINQStructAñadirReadOnlySpanAfterDisposeShouldThrowValueLinqSesionExpiradaException()
        {
            ValueLINQStruct<int> query = new(5);
            query.Añadir(10);
            query.Dispose();

            int[] elementsToAdd = [20, 30];
            try
            {
                query.Añadir(elementsToAdd.AsSpan());
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                _ = ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQRefStructAñadirReadOnlySpanAfterDisposeShouldThrowValueLinqSesionExpiradaException()
        {
            ValueLINQRefStruct<int> query = new(5);
            query.Añadir(10);
            query.Dispose();

            int[] elementsToAdd = [20, 30];
            try
            {
                query.Añadir(elementsToAdd.AsSpan());
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                _ = ex.Message.Should().Contain("expirado");
            }
        }

        #endregion

        #region 8. Pruebas de Materializadores Estándar (Nuevas)

        [Fact]
        public void ValueLINQStructToArrayStandardHappyPathShouldReturnExpectedElements()
        {
            int[] array = [10, 20, 30];
            ValueLINQStruct<int> query = array.ToValueQuery();

            int[] result = query.ToArrayStandard();

            _ = result.Should().Equal(array);
        }

        [Fact]
        public void ValueLINQStructToListStandardHappyPathShouldReturnExpectedElements()
        {
            int[] array = [10, 20, 30];
            ValueLINQStruct<int> query = array.ToValueQuery();

            List<int> result = query.ToListStandard();

            _ = result.Should().Equal(array);
        }

        [Fact]
        public void ValueLINQRefStructToArrayStandardHappyPathShouldReturnExpectedElements()
        {
            int[] array = [10, 20, 30];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();

            int[] result = query.ToArrayStandard();

            _ = result.Should().Equal(array);
        }

        [Fact]
        public void ValueLINQRefStructToListStandardHappyPathShouldReturnExpectedElements()
        {
            int[] array = [10, 20, 30];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();

            List<int> result = query.ToListStandard();

            _ = result.Should().Equal(array);
        }

        [Fact]
        public void ValueLINQStructToArrayStandardEmptySourceShouldReturnEmptyAndDispose()
        {
            int[] array = [];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long token = query.Token;

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void ValueLINQStructToListStandardEmptySourceShouldReturnEmptyAndDispose()
        {
            int[] array = [];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long token = query.Token;

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void ValueLINQRefStructToArrayStandardEmptySourceShouldReturnEmptyAndDispose()
        {
            int[] array = [];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();
            long token = query.Token;

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void ValueLINQRefStructToListStandardEmptySourceShouldReturnEmptyAndDispose()
        {
            int[] array = [];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();
            long token = query.Token;

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void ValueLINQStructStandardMaterializersWithZeroTokenShouldReturnEmpty()
        {
            ValueLINQStruct<int> queryArray = new(0L);
            ValueLINQStruct<int> queryLista = new(0L);

            _ = queryArray.ToArrayStandard().Should().BeEmpty();
            _ = queryLista.ToListStandard().Should().BeEmpty();
        }

        [Fact]
        public void ValueLINQRefStructStandardMaterializersWithZeroTokenShouldReturnEmpty()
        {
            ValueLINQRefStruct<int> queryArray = new(0L);
            ValueLINQRefStruct<int> queryLista = new(0L);

            _ = queryArray.ToArrayStandard().Should().BeEmpty();
            _ = queryLista.ToListStandard().Should().BeEmpty();
        }

        [Fact]
        public void ValueLINQStructToArrayStandardShouldDisposeSourceImmediately()
        {
            int[] array = [10, 20, 30];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long token = query.Token;

            int[] result = query.ToArrayStandard();

            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("ToArrayStandard should dispose the session");

            Action act = () => query.Añadir(42);
            _ = act.Should().Throw<ValueLinqSesionExpiradaException>().WithMessage("*expirado*");
        }

        [Fact]
        public void ValueLINQStructToListStandardShouldDisposeSourceImmediately()
        {
            int[] array = [10, 20, 30];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long token = query.Token;

            List<int> result = query.ToListStandard();

            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("ToListStandard should dispose the session");

            Action act = () => query.Añadir(42);
            _ = act.Should().Throw<ValueLinqSesionExpiradaException>().WithMessage("*expirado*");
        }

        [Fact]
        public void ValueLINQRefStructToArrayStandardShouldDisposeSourceImmediately()
        {
            int[] array = [10, 20, 30];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();
            long token = query.Token;

            _ = query.ToArrayStandard();

            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("ToArrayStandard should dispose the session");

            try
            {
                query.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                _ = ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQRefStructToListStandardShouldDisposeSourceImmediately()
        {
            int[] array = [10, 20, 30];
            ValueLINQRefStruct<int> query = array.ToValueRefQuery();
            long token = query.Token;

            _ = query.ToListStandard();

            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("ToListStandard should dispose the session");

            try
            {
                query.Añadir(42);
                Assert.Fail("Should have thrown ValueLinqSesionExpiradaException");
            }
            catch (ValueLinqSesionExpiradaException ex)
            {
                _ = ex.Message.Should().Contain("expirado");
            }
        }

        [Fact]
        public void ValueLINQStructToArrayStandardEmptyQueryShouldReturnEmptyArrayAndDispose()
        {
            ValueLINQStruct<int> query = Array.Empty<int>().ToValueQuery();
            long token = query.Token;

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = result.Should().BeSameAs([]);
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("Empty query session should be disposed after ToArrayStandard");
        }

        [Fact]
        public void ValueLINQStructToListStandardEmptyQueryShouldReturnEmptyListAndDispose()
        {
            ValueLINQStruct<int> query = Array.Empty<int>().ToValueQuery();
            long token = query.Token;

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("Empty query session should be disposed after ToListStandard");
        }

        [Fact]
        public void ValueLINQRefStructToArrayStandardEmptyQueryShouldReturnEmptyArrayAndDispose()
        {
            ValueLINQRefStruct<int> query = Array.Empty<int>().ToValueRefQuery();
            long token = query.Token;

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = result.Should().BeSameAs([]);
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("Empty query session should be disposed after ToArrayStandard");
        }

        [Fact]
        public void ValueLINQRefStructToListStandardEmptyQueryShouldReturnEmptyListAndDispose()
        {
            ValueLINQRefStruct<int> query = Array.Empty<int>().ToValueRefQuery();
            long token = query.Token;

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse("Empty query session should be disposed after ToListStandard");
        }

        [Fact]
        public void ValueLINQStructToArrayStandardInvalidTokenShouldReturnEmptyArraySafely()
        {
            ValueLINQStruct<int> query = new(); // Token is 0

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = result.Should().BeSameAs([]);
        }

        [Fact]
        public void ValueLINQStructToListStandardInvalidTokenShouldReturnEmptyListSafely()
        {
            ValueLINQStruct<int> query = new(); // Token is 0

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
        }

        [Fact]
        public void ValueLINQRefStructToArrayStandardInvalidTokenShouldReturnEmptyArraySafely()
        {
            ValueLINQRefStruct<int> query = new(); // Token is 0

            int[] result = query.ToArrayStandard();

            _ = result.Should().BeEmpty();
            _ = result.Should().BeSameAs([]);
        }

        [Fact]
        public void ValueLINQRefStructToListStandardInvalidTokenShouldReturnEmptyListSafely()
        {
            ValueLINQRefStruct<int> query = new(); // Token is 0

            List<int> result = query.ToListStandard();

            _ = result.Should().BeEmpty();
        }

        #endregion
    }
}
