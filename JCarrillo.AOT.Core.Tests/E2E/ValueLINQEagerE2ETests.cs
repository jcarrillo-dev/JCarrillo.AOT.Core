using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using JCarrillo.AOT.Core.Extensiones.ArrayPool;
using JCarrillo.AOT.Core.Colecciones.Pooled;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ValueLINQEagerE2ETests
    {
        static ValueLINQEagerE2ETests()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQGC).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQStateManager<int>).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQStateManager<byte>).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQStateManager<string>).TypeHandle);
        }

        #region Structs de Prueba para ValueLINQ

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
            public readonly bool Ejecutar(int item, int otro) => item == 2 ? throw new InvalidOperationException("Error simulado") : true;
        }

        private struct ThrowingSelector : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item == 2 ? throw new InvalidOperationException("Error simulado") : item;
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

        private static int GetActiveSlotsCount<TItem>()
            => ValueLINQConfig.TamañoTabla - ValueLINQStateManager<TItem>.SlotsLibres;

        #endregion

        // Nivel 1: Cobertura de Características — Característica 1: ValueLINQ Eager

        [Fact]
        public void ValueLINQStructCreationAndDisposalSucceeds()
        {
            // Preparar
            int[] array = [1, 2, 3];
            int initialSlots = GetActiveSlotsCount<int>();

            // Actuar
            ValueLINQStruct<int> query = array.ToValueQuery();

            // Aserción
            _ = query.IsValido.Should().BeTrue();
            _ = GetActiveSlotsCount<int>().Should().Be(initialSlots + 1);

            // Actuar
            query.Dispose();

            // Aserción
            _ = query.IsValido.Should().BeFalse();
            _ = GetActiveSlotsCount<int>().Should().Be(initialSlots);
        }

        [Fact]
        public void ValueLINQStructWhereFiltersElementsCorrectly()
        {
            // Preparar
            int[] array = [1, 2, 3, 2, 4];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where(2, new IntEqualsPredicate());

            // Aserción
            _ = filtered.IsValido.Should().BeTrue();
            PooledArray<int> result = filtered.ToArray();
            _ = result.Span.ToArray().Should().Equal(2, 2);
        }

        [Fact]
        public void ValueLINQStructSelectProjectsElementsCorrectly()
        {
            // Preparar
            int[] array = [1, 2, 3];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> projected = query.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());

            // Aserción
            _ = projected.IsValido.Should().BeTrue();
            PooledArray<int> result = projected.ToArray();
            _ = result.Span.ToArray().Should().Equal(2, 4, 6);
        }

        [Fact]
        public void ValueLINQStructChunkSplitsIntoCorrectSizes()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            // Aserción
            _ = chunks.IsValido.Should().BeTrue();
            PooledList<ValueLINQStruct<int>> chunkList = chunks.ToList();
            _ = chunkList.Tamaño.Should().Be(3);

            _ = chunkList[0].ToArray().Span.ToArray().Should().Equal(1, 2);
            _ = chunkList[1].ToArray().Span.ToArray().Should().Equal(3, 4);
            _ = chunkList[2].ToArray().Span.ToArray().Should().Equal(5);

            // Desechar los chunks internos
            for (int i = 0; i < chunkList.Tamaño; i++)
            {
                chunkList[i].Dispose();
            }
        }

        [Fact]
        public void ValueLINQStructMaterializationProducesCorrectCollections()
        {
            // Preparar
            int[] array = [1, 2, 3];

            // Actuar y Aserción
            using (ValueLINQStruct<int> q1 = array.ToValueQuery())
            {
                using PooledList<int> list = q1.ToList();
                _ = list.Span.ToArray().Should().Equal(1, 2, 3);
            }

            using ValueLINQStruct<int> q2 = array.ToValueQuery();
            using PooledArray<int> arr = q2.ToArray();
            _ = arr.Span.ToArray().Should().Equal(1, 2, 3);
        }

        // Nivel 1: Cobertura de Características — Característica 3: ValueLINQGC / StateManager

        [Fact]
        public void ValueLINQStateManagerMonotonicVersionPreventsABA()
        {
            // Preparar
            int[] array = [42];

            // Rentar un slot
            ValueLINQStruct<int> query1 = array.ToValueQuery();
            long token1 = query1.Token;
            int slot1 = TokenHelper.ObtenerSlotIndex(token1);
            long version1 = TokenHelper.ObtenerVersion(token1);

            // Actuar
            query1.Dispose();

            // Rentar de nuevo
            ValueLINQStruct<int> query2 = array.ToValueQuery();
            long token2 = query2.Token;
            int slot2 = TokenHelper.ObtenerSlotIndex(token2);
            long version2 = TokenHelper.ObtenerVersion(token2);

            query2.Dispose();

            // Aserción
            _ = slot2.Should().Be(slot1, "debe reutilizar el mismo slot");
            _ = version2.Should().Be(version1 + 1, "la versión debe incrementarse para prevenir ABA");
        }

        [Fact]
        public void ValueLINQStateManagerTokenHelperPacksAndUnpacksCorrectly()
        {
            // Preparar
            int slot = 256;
            long version = 98765L;

            // Actuar
            long token = TokenHelper.CrearToken(slot, version);

            // Aserción
            _ = TokenHelper.ObtenerSlotIndex(token).Should().Be(slot);
            _ = TokenHelper.ObtenerVersion(token).Should().Be(version);
        }

        [Fact]
        public void ValueLINQStateManagerSessionExpiredExceptionThrowsOnExpiredToken()
        {
            // Preparar
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> query = array.ToValueQuery();
            long token = query.Token;
            query.Dispose();

            // Actuar y Aserción
            Action act = () => ValueLINQStateManager<int>.ObtenerMetadatos(token);
            _ = act.Should().Throw<ValueLinqSesionExpiradaException>();
        }

        [Fact]
        public void ValueLINQStateManagerTokenInvalidoExceptionThrowsOnInvalidToken()
        {
            // Actuar y Aserción
            Action act = () => ValueLINQStateManager<int>.ObtenerMetadatos(0L);
            _ = act.Should().Throw<ValueLinqTokenInvalidoException>();
        }

        private static readonly int[] origen = [1];
        private static readonly double[] origenDouble = [1.0];
        private static readonly string[] origenArray = ["a"];

        [Fact]
        public void ValueLINQGCSingleGlobalTimerCleansAllStateManagers()
        {
            // Preparar
            ValueLINQStruct<int> qInt = origen.ToValueQuery();
            ValueLINQStruct<string> qStr = origenArray.ToValueQuery();
            ValueLINQStruct<double> qDouble = origenDouble.ToValueQuery();

            long tInt = qInt.Token;
            long tStr = qStr.Token;
            long tDouble = qDouble.Token;

            // Usar reflexión para saltar el límite público de 1 minuto de TiempoLimpieza
            FieldInfo? fieldInt = typeof(ValueLINQStateManager<int>).GetField("_tiempoLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo? fieldStr = typeof(ValueLINQStateManager<string>).GetField("_tiempoLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo? fieldDouble = typeof(ValueLINQStateManager<double>).GetField("_tiempoLimpieza", BindingFlags.NonPublic | BindingFlags.Static);

            fieldInt!.SetValue(null, TimeSpan.Zero);
            fieldStr!.SetValue(null, TimeSpan.Zero);
            fieldDouble!.SetValue(null, TimeSpan.Zero);

            // Actuar
            // Desencadenar la limpieza manualmente mediante reflexión
            MethodInfo? cleanupMethod = typeof(ValueLINQGC).GetMethod("EjecutarLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            _ = cleanupMethod!.Invoke(null, null);

            // Aserción
            _ = ValueLINQStateManager<int>.IsMetadatoValido(tInt).Should().BeFalse();
            _ = ValueLINQStateManager<string>.IsMetadatoValido(tStr).Should().BeFalse();
            _ = ValueLINQStateManager<double>.IsMetadatoValido(tDouble).Should().BeFalse();

            // Verificar que existe un único timer global en ValueLINQGC
            FieldInfo? timerField = typeof(ValueLINQGC).GetField("_timer", BindingFlags.NonPublic | BindingFlags.Static);
            _ = timerField!.GetValue(null).Should().NotBeNull();
        }

        private static readonly int[] origenTres = [1, 2, 3];

        // Nivel 2: Casos Límite y Extremos — Característica 1: ValueLINQ Eager

        [Fact]
        public void ValueLINQStructChunkZeroOrNegativeSizeThrowsException()
        {
            // Preparar
            using ValueLINQStruct<int> query = origenTres.ToValueQuery();

            // Actuar y Aserción
            Action act0 = () => query.Chunk(0);
            _ = act0.Should().Throw<ArgumentOutOfRangeException>();

            Action actNeg = () => query.Chunk(-5);
            _ = actNeg.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ValueLINQStructOperationsOnDefaultStructReturnsEmptyQuery()
        {
            // Preparar
            ValueLINQStruct<int> query = default;

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where(1, new IntEqualsPredicate());

            // Aserción
            _ = filtered.IsValido.Should().BeTrue();
            using PooledArray<int> array = filtered.ToArray();
            _ = array.Tamaño.Should().Be(0);
        }

        [Fact]
        public void ValueLINQStructWherePredicateThrowsPreventsLeak()
        {
            // Preparar
            int[] array = [1, 2, 3];
            int slotsBefore = GetActiveSlotsCount<int>();
            ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            Action act = () => query.Where(0, new ThrowingPredicate());

            // Aserción
            _ = act.Should().Throw<InvalidOperationException>();
            _ = GetActiveSlotsCount<int>().Should().Be(slotsBefore);
        }

        [Fact]
        public void ValueLINQStructSelectSelectorThrowsPreventsLeak()
        {
            // Preparar
            int[] array = [1, 2, 3];
            int slotsBefore = GetActiveSlotsCount<int>();
            ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            Action act = () => query.Select<int, ThrowingSelector, int>(new ThrowingSelector());

            // Aserción
            _ = act.Should().Throw<InvalidOperationException>();
            _ = GetActiveSlotsCount<int>().Should().Be(slotsBefore);
        }

        [Fact]
        public void ValueLINQStructDoubleDisposeIsSafeNoOp()
        {
            // Preparar
            ValueLINQStruct<int> query = origenTres.ToValueQuery();

            // Actuar y Aserción
            Action act = () =>
            {
                query.Dispose();
                query.Dispose();
            };
            _ = act.Should().NotThrow();
        }

        // Nivel 2: Casos Límite y Extremos — Característica 3: ValueLINQGC / StateManager

        [Fact]
        public void ValueLINQStateManagerCapacityReachedThrowsInvalidOperationException()
        {
            // Preparar
            ValueLINQStruct<int>[] queries = new ValueLINQStruct<int>[4096];
            try
            {
                for (int i = 0; i < 4096; i++)
                {
                    queries[i] = new ValueLINQStruct<int>(1);
                }

                // Actuar y Aserción
                Action act = () => _ = new ValueLINQStruct<int>(1);
                _ = act.Should().Throw<InvalidOperationException>()
                   .WithMessage("*Capacidad*máxima*");
            }
            finally
            {
                for (int i = 0; i < 4096; i++)
                {
                    queries[i].Dispose();
                }
            }
        }

        [Fact]
        public void ValueLINQGCSetInvalidLimpiezaIntervalThrowsArgumentOutOfRangeException()
        {
            // Actuar y Aserción
            Action act = () => ValueLINQStateManager<int>.TiempoLimpieza = TimeSpan.FromSeconds(30);
            _ = act.Should().Throw<ArgumentOutOfRangeException>()
               .WithMessage("*mínimo*");
        }

        [Fact]
        public async Task ValueLINQStateManagerUnderHighConcurrencyDoesNotCorruptStack()
        {
            // Preparar
            const int threadCount = 100;
            const int iterations = 100;
            Task[] tasks = new Task[threadCount];
            int initialSlots = GetActiveSlotsCount<int>();

            // Actuar
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        using ValueLINQStruct<int> query = new(10);
                        _ = query.IsValido.Should().BeTrue();
                    }
                });
            }
            await Task.WhenAll(tasks);

            // Aserción
            _ = GetActiveSlotsCount<int>().Should().Be(initialSlots);
        }

        [Fact]
        public void ValueLINQStateManagerAccessTokenWithInvalidIndexThrowsTokenInvalidoException()
        {
            // Actuar y Aserción
            Action act = () => ValueLINQStateManager<int>.ObtenerMetadatos(0L);
            _ = act.Should().Throw<ValueLinqTokenInvalidoException>();
        }

        private static readonly string[] origenTexto = ["hola"];

        [Fact]
        public void ValueLINQStateManagerCleanUpExpiredSessionReturnsArrayToPool()
        {
            // Preparar
            _ = ArrayPool<string>.Shared;
            ValueLINQStruct<string> query = origenTexto.ToValueQuery();
            long token = query.Token;

            // Marcar como expirado
            FieldInfo? field = typeof(ValueLINQStateManager<string>).GetField("_tiempoLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, TimeSpan.Zero);

            // Actuar
            MethodInfo? cleanupMethod = typeof(ValueLINQGC).GetMethod("EjecutarLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            _ = cleanupMethod!.Invoke(null, null);

            // Aserción
            _ = ValueLINQStateManager<string>.IsMetadatoValido(token).Should().BeFalse();
        }

        // Nivel 3: Combinaciones de Características Cruzadas (Cobertura por Pares)

        [Fact]
        public void CombinationC1EagerQueryInsideSemaphoreWithArrayPool()
        {
            using SemaphoreSlim semaphore = new(1, 1);
            int[] source = [1, 2, 3, 4, 5];
            int firstElement = 0;

            AllocationAssert.AssertZeroAllocations(() =>
            {
                using PooledList<int> pooledList = ArrayPool<int>.Shared.ObtenerLista(source.Length);
                for (int i = 0; i < source.Length; i++)
                {
                    pooledList[i] = source[i];
                }

                using SemaphoreLock semLock = semaphore.Esperar();

                using ValueLINQStruct<int> query = pooledList.Span.ToValueQuery();
                using ValueLINQStruct<int> filtered = query.Where(3, new IntEqualsPredicate());
                using ValueLINQStruct<int> projected = filtered.Select<int, IntDoubleSelector, int>(new IntDoubleSelector());
                using PooledArray<int> result = projected.ToArray();

                firstElement = result.Span[0];
            });

            _ = firstElement.Should().Be(6);
        }

        [Fact]
        public void CombinationC4EagerQueryWithExceptionAndGCCleanup()
        {
            int[] array = [1, 2, 3];
            int slotsBefore = GetActiveSlotsCount<int>();

            // Actuar 1: Desencadenar excepción dentro de la consulta para verificar la liberación inmediata
            Action act = () =>
            {
                ValueLINQStruct<int> query = array.ToValueQuery();
                _ = query.Where(0, new ThrowingPredicate());
            };
            _ = act.Should().Throw<InvalidOperationException>();
            _ = GetActiveSlotsCount<int>().Should().Be(slotsBefore);

            // Actuar 2: Simular sesión huérfana y verificar que la limpieza del GC la recolecta
            ValueLINQStruct<int> queryOrphaned = array.ToValueQuery();
            long token = queryOrphaned.Token;

            // Establecer el tiempo de limpieza a cero
            FieldInfo? field = typeof(ValueLINQStateManager<int>).GetField("_tiempoLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            field!.SetValue(null, TimeSpan.Zero);

            MethodInfo? cleanupMethod = typeof(ValueLINQGC).GetMethod("EjecutarLimpieza", BindingFlags.NonPublic | BindingFlags.Static);
            _ = cleanupMethod!.Invoke(null, null);

            // Aserción
            _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
            _ = GetActiveSlotsCount<int>().Should().Be(slotsBefore);
        }

        // Nivel 4: Escenarios de Aplicación del Mundo Real

        public struct LogEvent
        {
            internal string Message;
            internal int Level;
        }

        public struct LogLevelPredicate : IWhereDelegado<string, int>
        {
            // nivel 2 = ERROR
            public readonly bool Ejecutar(string item, int level) =>
                level == 2 && item.Contains("[ERROR]");
        }

        public struct LogParserSelector : ISelectDelegado<string, LogEvent>
        {
            public LogEvent Ejecutar(string item) => new() { Message = item, Level = 2 };
        }

        public readonly struct LogBatchProcessor(SemaphoreSlim semaphore) : IProcesarChunkDelegado<LogEvent>
        {
            private readonly SemaphoreSlim _semaphore = semaphore;

            public void Ejecutar(ValueLINQStruct<LogEvent> listaChunk)
            {
                using SemaphoreLock l = _semaphore.Esperar();
                int count = 0;
                foreach (ref LogEvent item in listaChunk)
                {
                    count++;
                }
            }
        }

        [Fact]
        public void ScenarioLogParsingSucceedsWithZeroAllocations()
        {
            string[] logs = new string[1000];
            for (int i = 0; i < 1000; i++)
            {
                logs[i] = i % 10 == 0 ? "[ERROR] Algo salió mal" : "[INFO] Todo bien";
            }

            using SemaphoreSlim semaphore = new(1, 1);

            AllocationAssert.AssertZeroAllocations(() =>
            {
                using PooledList<string> pooledLogs = ArrayPool<string>.Shared.ObtenerLista(logs.Length);
                for (int i = 0; i < logs.Length; i++)
                {
                    pooledLogs[i] = logs[i];
                }

                using ValueLINQStruct<string> query = pooledLogs.Span.ToValueQuery();
                using ValueLINQStruct<string> errors = query.Where(2, new LogLevelPredicate()); // 2 = ERROR
                using ValueLINQStruct<LogEvent> events = errors.Select<string, LogParserSelector, LogEvent>(new LogParserSelector());
                using ValueLINQRefStruct<ValueLINQStruct<LogEvent>> chunks = events.Chunk(100);

                chunks.ProcessChunks(new LogBatchProcessor(semaphore));
            });
        }

        [Fact]
        public void ScenarioNetworkPacketProcessingSucceedsWithZeroAllocations()
        {
            byte[] segment1 = [1, 2, 3];
            byte[] segment2 = [4, 5, 6];
            byte[] segment3 = [7, 8, 9];
            byte checksum = 0;

            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQRefStruct<byte> q1 = segment1.AsSpan().ToValueRefQuery();
                using ValueLINQRefStruct<byte> q2 = segment2.AsSpan().ToValueRefQuery();
                using ValueLINQRefStruct<byte> q3 = segment3.AsSpan().ToValueRefQuery();

                using ValueLINQRefStruct<byte> concatenated = q1.Concat(q2).Concat(q3);

                // Calcular checksum
                byte localChecksum = 0;
                ref MetadatosSesion<byte> metadatos = ref ValueLINQStateManager<byte>.ObtenerMetadatos(concatenated.Token);
                for (int i = 0; i < metadatos.TamañoActual; i++)
                {
                    localChecksum ^= metadatos.Array![i];
                }
                checksum = localChecksum;
            });

            _ = checksum.Should().Be(1 ^ 2 ^ 3 ^ 4 ^ 5 ^ 6 ^ 7 ^ 8 ^ 9);
        }

        [Fact]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:RequiresUnreferencedCode",
            Justification = "Prueba de barandillas arquitectónicas que inspecciona dinámicamente tipos para hacer cumplir invariantes de diseño.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2075:UnrecognizedReflectionPattern",
            Justification = "Prueba de barandillas arquitectónicas que inspecciona dinámicamente GetEnumerator para verificar que retornan structs.")]
        public void ScenarioArchitecturalGuardrailsSucceeds()
        {
            Assembly assembly = typeof(ValueLINQGC).Assembly;

            // 1. Verificar que las clases de control interno y excepciones están selladas (sealed)
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.IsClass && !type.IsAbstract)
                {
                    if (typeof(Exception).IsAssignableFrom(type) ||
                        type.Name.Contains("ValueLINQGC") ||
                        type.Name.Contains("ValueLINQStateManager"))
                    {
                        _ = type.Should().BeSealed($"{type.Name} debe estar sellada para prevenir sobrecosto de herencia");
                    }
                }
            }

            // 2. Verificar que los enumeradores de struct implementan GetEnumerator basado en patrones retornando un struct
            IEnumerable<Type> structTypes = types.Where(t => t.IsValueType && !t.IsEnum);
            foreach (Type? type in structTypes)
            {
                MethodInfo? getEnumeratorMethod = type.GetMethod("GetEnumerator");
                if (getEnumeratorMethod != null)
                {
                    _ = (getEnumeratorMethod.ReturnType.IsValueType || getEnumeratorMethod.ReturnType.IsGenericParameter).Should().BeTrue(
                        $"{type.Name}.GetEnumerator() debe retornar un struct o un parámetro genérico para evitar boxing");
                }
            }
        }
    }
}
