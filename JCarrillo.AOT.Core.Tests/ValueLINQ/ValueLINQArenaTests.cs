using System;
using FluentAssertions;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class ValueLINQArenaTests
    {
        private struct PredicadoMayorQue : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int umbral) => item > umbral;
        }

        private struct SelectorDoble : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        [Fact]
        public void ToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] datos = [1, 2, 3];
            Action act = () => datos.ToValueQuery(default);

            _ = act.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(0);
        }

        [Fact]
        public void ToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] datos = [1, 2, 3];
            Action act = () => datos.ToValueRefQuery(default);

            _ = act.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(0);
        }

        [Fact]
        public void ToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            Action act = () => datos.ToValueQuery(arena);

            _ = act.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idEsperado);
        }

        [Fact]
        public void ToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            Action act = () => datos.ToValueRefQuery(arena);

            _ = act.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idEsperado);
        }

        [Fact]
        public void ToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] datos = [1, 2, 3, 4, 5];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using ValueLINQStruct<int> query = datos.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            using ValueLINQStruct<int> filtrado = query.Where(2, new PredicadoMayorQue());
            _ = filtrado.IsValido.Should().BeTrue();
            _ = filtrado.TokenArena.Should().Be(arena.TokenArena);

            using ValueLINQStruct<int> proyectado = filtrado.Select<int, SelectorDoble, int>(new SelectorDoble());
            _ = proyectado.IsValido.Should().BeTrue();
            _ = proyectado.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in proyectado)
                suma += x;

            _ = suma.Should().Be((3 * 2) + (4 * 2) + (5 * 2));
        }

        [Fact]
        public void ToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] datos = [1, 2, 3, 4, 5];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQRefStruct<int> query = datos.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            ValueLINQRefStruct<int> filtrado = query.Where(2, new PredicadoMayorQue());
            _ = filtrado.IsValido.Should().BeTrue();
            _ = filtrado.TokenArena.Should().Be(arena.TokenArena);

            ValueLINQRefStruct<int> proyectado = filtrado.Select<int, SelectorDoble, int>(new SelectorDoble());
            _ = proyectado.IsValido.Should().BeTrue();
            _ = proyectado.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in proyectado)
                suma += x;

            _ = suma.Should().Be((3 * 2) + (4 * 2) + (5 * 2));
        }

        [Fact]
        public void DisponerArenaInvalidaTantoQueryComoRefQuery()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQStruct<int> query = datos.ToValueQuery(arena);
            ValueLINQRefStruct<int> refQuery = datos.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = refQuery.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = query.IsValido.Should().BeFalse();
            _ = refQuery.IsValido.Should().BeFalse();
        }

        [Fact]
        public void ObtenerMetadatosConTokenArenaGeneracionCruzadaLanzaArenaInactiva()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            int id = arena.Id;
            long tokenVivo = arena.TokenArena;
            long generacionActual = TokenHelper.ObtenerGeneracionTokenArena(tokenVivo);

            long tokenGeneracionAnterior = TokenHelper.CrearTokenArena(id, generacionActual - 1);

            Action act = () => _ = ValueLINQStateManager<int>.ObtenerMetadatos(tokenGeneracionAnterior, 4);

            _ = act.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(id);
        }

        [Fact]
        public void ObtenerTablaConTokenSesionGeneracionCruzadaLanzaArenaInactiva()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            int id = arena.Id;
            long tokenVivo = arena.TokenArena;
            long generacionActual = TokenHelper.ObtenerGeneracionTokenArena(tokenVivo);

            // Crear token de sesión forjado con generación anterior para la arena viva
            long tokenSesionObsoleto = TokenHelper.CrearToken(0, id, generacionActual - 1, 1L);

            Action actAñadir = () => ValueLINQStateManager<int>.Añadir(tokenSesionObsoleto, 42);
            Action actAsegurar = () => ValueLINQStateManager<int>.AsegurarEspacio(tokenSesionObsoleto, 16);

            _ = actAñadir.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(id);
            _ = actAsegurar.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(id);
            _ = ValueLINQStateManager<int>.IsMetadatoValido(tokenSesionObsoleto).Should().BeFalse();
        }

        [Fact]
        public void ToValueQueryConOrigenNullLanzaArgumentNullExceptionAntesDeValidarArena()
        {
            int[]? datos = null;
            Action act = () => datos!.ToValueQuery(default);

            _ = act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ToValueRefQueryConOrigenNullLanzaArgumentNullExceptionAntesDeValidarArena()
        {
            int[]? datos = null;
            Action act = () => datos!.ToValueRefQuery(default);

            _ = act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void ColisionArenaRecicladaConNuevaGeneracionBloqueaHandlesObsoletos()
        {
            // 1. Crear Arena A (slot S, gen G) y crear consultas ValueLINQStruct y ValueLINQRefStruct
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            int slotS = arenaA.Id;
            long genA = TokenHelper.ObtenerGeneracionTokenArena(arenaA.TokenArena);

            int[] datosA = [10, 20, 30];
            ValueLINQStruct<int> queryA = datosA.ToValueQuery(arenaA);
            ValueLINQRefStruct<int> refQueryA = datosA.ToValueRefQuery(arenaA);

            queryA.IsValido.Should().BeTrue();
            refQueryA.IsValido.Should().BeTrue();

            // 2. Disponer Arena A: la tabla de la arena se limpia y slot S se libera al pool de ids
            arenaA.Dispose();

            arenaA.IsViva.Should().BeFalse();
            queryA.IsValido.Should().BeFalse();
            refQueryA.IsValido.Should().BeFalse();

            // 3. Reciclar ranura S en una nueva Arena B (gen G + 1)
            System.Collections.Generic.List<ValueLINQArena> intermedias = new();
            ValueLINQArena arenaB = default;
            try
            {
                while (true)
                {
                    ValueLINQArena candidata = ValueLINQArena.Crear();
                    if (candidata.Id == slotS)
                    {
                        arenaB = candidata;
                        break;
                    }
                    intermedias.Add(candidata);
                }

                arenaB.Id.Should().Be(slotS);
                long genB = TokenHelper.ObtenerGeneracionTokenArena(arenaB.TokenArena);
                genB.Should().Be(genA + 1);

                // 4. Arena B crea sus propias consultas con datos diferentes en el mismo slot S
                int[] datosB = [999, 888, 777];
                using ValueLINQStruct<int> queryB = datosB.ToValueQuery(arenaB);

                queryB.IsValido.Should().BeTrue();
                queryB.TokenArena.Should().Be(arenaB.TokenArena);

                // 5. Ataques adversariales desde los handles obsoletos de Arena A
                // Vector A: queryA.IsValido y refQueryA.IsValido deben ser false (no ven la sesión de Arena B)
                queryA.IsValido.Should().BeFalse();
                refQueryA.IsValido.Should().BeFalse();

                // Vector B: Enumerar queryA obsoleta lanza ValueLinqArenaInactivaException
                Action actEnumA = () =>
                {
                    foreach (int _ in queryA) { }
                };
                actEnumA.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector C: Enumerar refQueryA obsoleta lanza ValueLinqArenaInactivaException
                bool haLanzadoRefEnum = false;
                try
                {
                    foreach (int _ in refQueryA) { }
                }
                catch (ValueLinqArenaInactivaException ex)
                {
                    haLanzadoRefEnum = true;
                    ex.IdArena.Should().Be(slotS);
                }
                haLanzadoRefEnum.Should().BeTrue();

                // Vector D: Mutar queryA mediante Añadir() lanza ValueLinqArenaInactivaException
                Action actAñadir = () => queryA.Añadir(555);
                actAñadir.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector E: AsegurarEspacio en StateManager con token obsoleto lanza ValueLinqArenaInactivaException
                Action actAsegurar = () => ValueLINQStateManager<int>.AsegurarEspacio(queryA.Token, 64);
                actAsegurar.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector F: ObtenerMetadatos con TokenArena obsoleto lanza ValueLinqArenaInactivaException
                Action actObtenerMetaArena = () => _ = ValueLINQStateManager<int>.ObtenerMetadatos(queryA.TokenArena, 16);
                actObtenerMetaArena.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector G: Encadenar operadores (Where) sobre queryA obsoleta lanza ValueLinqArenaInactivaException
                Action actWhere = () => _ = queryA.Where(10, new PredicadoMayorQue());
                actWhere.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector H: Disponer queryA obsoleta es seguro (no-op) y NO libera la ranura de Arena B
                Action actDispose = () => queryA.Dispose();
                actDispose.Should().NotThrow();

                // Vector J: Recrear ValueLINQStruct o ValueLINQRefStruct con token de sesión obsoleto lanza ValueLinqArenaInactivaException
                Action actConstructStale = () => _ = new ValueLINQStruct<int>(queryA.Token);
                actConstructStale.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                Action actConstructRefStale = () => _ = new ValueLINQRefStruct<int>(queryA.Token);
                actConstructRefStale.Should().Throw<ValueLinqArenaInactivaException>()
                    .Which.IdArena.Should().Be(slotS);

                // Vector K: Certificar que los datos de Arena B están intactos e incorruptos
                queryB.IsValido.Should().BeTrue();
                int sumaB = 0;
                foreach (int x in queryB)
                    sumaB += x;
                sumaB.Should().Be(999 + 888 + 777);
            }
            finally
            {
                if (arenaB.IsViva)
                    arenaB.Dispose();

                foreach (ValueLINQArena intermedia in intermedias)
                    if (intermedia.IsViva)
                        intermedia.Dispose();
            }
        }

        #region Adversarial Empirical Probes

        [Fact]
        public void EmpiricalProbe1DefaultArenaNoPuedeAccederArena0NiAfectarAmbiente()
        {
            ValueLINQArena defaultArena = default;

            // 1. Validar que las propiedades de default(ValueLINQArena) reflejan estado inactivo
            defaultArena.IsViva.Should().BeFalse();
            defaultArena.Id.Should().Be(0);
            defaultArena.TokenArena.Should().Be(0L);

            // 2. Validar que el gestor de arenas rechaza el token 0L
            ValueLINQArenaManager.IsArenaViva(defaultArena.TokenArena).Should().BeFalse();

            // 3. Validar que el gestor de estados lanza excepción al intentar obtener metadatos con token 0L
            Action actObtenerMeta = () => _ = ValueLINQStateManager<int>.ObtenerMetadatos(defaultArena.TokenArena, 4);
            actObtenerMeta.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(0);

            // 4. Validar que Dispose() sobre default(ValueLINQArena) es no-op seguro y no altera la arena ambiente 0
            Action actDispose = () =>
            {
                defaultArena.Dispose();
                defaultArena.Dispose();
            };
            actDispose.Should().NotThrow();

            // 5. Validar que la arena ambiente sigue perfectamente operativa tras el Dispose() de default
            int[] datosAmbiente = [10, 20, 30];
            using ValueLINQStruct<int> queryAmbiente = datosAmbiente.ToValueQuery();
            queryAmbiente.IsValido.Should().BeTrue();
            queryAmbiente.TokenArena.Should().Be(0L);
            int suma = 0;
            foreach (int item in queryAmbiente)
                suma += item;
            suma.Should().Be(60);
        }

        [Fact]
        public void EmpiricalProbe2ChainingConDefaultArenaFallaEnOrigenYDefaultStructEsSeguro()
        {
            int[] datos = [1, 2, 3, 4, 5];

            // 1. Chaining con ToValueQuery(default) falla inmediatamente al inicio sin ejecutar operadores
            Action actChainStruct = () => datos.ToValueQuery(default)
                .Where(2, new PredicadoMayorQue())
                .Select<int, SelectorDoble, int>(new SelectorDoble());

            actChainStruct.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(0);

            // 2. Chaining con ToValueRefQuery(default) falla inmediatamente al inicio
            Action actChainRef = () => datos.ToValueRefQuery(default)
                .Where(2, new PredicadoMayorQue())
                .Select<int, SelectorDoble, int>(new SelectorDoble());

            actChainRef.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(0);

            // 3. Chaining sobre default(ValueLINQStruct<int>) (instancia vacía no inicializada)
            ValueLINQStruct<int> defaultStruct = default;
            using ValueLINQStruct<int> structChained = defaultStruct
                .Where(2, new PredicadoMayorQue())
                .Select<int, SelectorDoble, int>(new SelectorDoble());

            structChained.IsValido.Should().BeTrue();
            structChained.TokenArena.Should().Be(0L);
            int countStruct = 0;
            foreach (int _ in structChained)
                countStruct++;
            countStruct.Should().Be(0);

            // 4. Chaining sobre default(ValueLINQRefStruct<int>) (instancia vacía no inicializada)
            ValueLINQRefStruct<int> defaultRefStruct = default;
            ValueLINQRefStruct<int> refStructChained = defaultRefStruct
                .Where(2, new PredicadoMayorQue())
                .Select<int, SelectorDoble, int>(new SelectorDoble());

            refStructChained.IsValido.Should().BeTrue();
            refStructChained.TokenArena.Should().Be(0L);
            int countRef = 0;
            foreach (int _ in refStructChained)
                countRef++;
            countRef.Should().Be(0);
        }

        [Fact]
        public void EmpiricalProbe3ArenaDispuestaJustoAntesDeToValueQueryYRefQuery()
        {
            int[] datos = [5, 10, 15];

            // 1. Crear y disponer inmediatamente antes de invocar ToValueQuery
            ValueLINQArena arenaStruct = ValueLINQArena.Crear();
            int idStruct = arenaStruct.Id;
            arenaStruct.Dispose();

            arenaStruct.IsViva.Should().BeFalse();

            Action actQuery = () => datos.ToValueQuery(arenaStruct);
            actQuery.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idStruct);

            // 2. Crear y disponer inmediatamente antes de invocar ToValueRefQuery
            ValueLINQArena arenaRef = ValueLINQArena.Crear();
            int idRef = arenaRef.Id;
            arenaRef.Dispose();

            arenaRef.IsViva.Should().BeFalse();

            Action actRefQuery = () => datos.ToValueRefQuery(arenaRef);
            actRefQuery.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idRef);
        }

        [Fact]
        public void EmpiricalProbe4PrecedenciaOrigenNullVsArenaInactiva()
        {
            int[]? datosNulos = null;

            // 1. Origen null + arena default -> ArgumentNullException
            Action actNullDefault = () => datosNulos!.ToValueQuery(default);
            actNullDefault.Should().Throw<ArgumentNullException>();

            Action actRefNullDefault = () => datosNulos!.ToValueRefQuery(default);
            actRefNullDefault.Should().Throw<ArgumentNullException>();

            // 2. Origen null + arena dispuesta -> ArgumentNullException
            ValueLINQArena arenaDispuesta = ValueLINQArena.Crear();
            arenaDispuesta.Dispose();

            Action actNullDispuesta = () => datosNulos!.ToValueQuery(arenaDispuesta);
            actNullDispuesta.Should().Throw<ArgumentNullException>();

            Action actRefNullDispuesta = () => datosNulos!.ToValueRefQuery(arenaDispuesta);
            actRefNullDispuesta.Should().Throw<ArgumentNullException>();

            // 3. Origen null + arena viva -> ArgumentNullException
            using ValueLINQArena arenaViva = ValueLINQArena.Crear();

            Action actNullViva = () => datosNulos!.ToValueQuery(arenaViva);
            actNullViva.Should().Throw<ArgumentNullException>();

            Action actRefNullViva = () => datosNulos!.ToValueRefQuery(arenaViva);
            actRefNullViva.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void EmpiricalProbe5PropagacionTokenArenaEnChunkYConcat()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            int[] datos = [1, 2, 3, 4, 5, 6];

            // 1. Chunk propaga TokenArena tanto al contenedor como a los sub-chunks
            ValueLINQRefStruct<int> refQuery = datos.ToValueRefQuery(arena);
            ValueLINQRefStruct<ValueLINQStruct<int>> chunks = refQuery.Chunk(2);

            chunks.IsValido.Should().BeTrue();
            chunks.TokenArena.Should().Be(arena.TokenArena);

            int totalElementos = 0;
            foreach (ValueLINQStruct<int> chunk in chunks)
            {
                chunk.IsValido.Should().BeTrue();
                chunk.TokenArena.Should().Be(arena.TokenArena);
                foreach (int elem in chunk)
                    totalElementos += elem;
            }
            totalElementos.Should().Be(1 + 2 + 3 + 4 + 5 + 6);

            // 2. Concat de dos consultas de la misma arena propaga TokenArena
            using ValueLINQStruct<int> q1 = datos.ToValueQuery(arena);
            using ValueLINQStruct<int> q2 = datos.ToValueQuery(arena);
            using ValueLINQStruct<int> conc = q1.Concat(q2);

            conc.IsValido.Should().BeTrue();
            conc.TokenArena.Should().Be(arena.TokenArena);

            int sumaConcat = 0;
            foreach (int item in conc)
                sumaConcat += item;
            sumaConcat.Should().Be((1 + 2 + 3 + 4 + 5 + 6) * 2);
        }

        #endregion
    }
}

