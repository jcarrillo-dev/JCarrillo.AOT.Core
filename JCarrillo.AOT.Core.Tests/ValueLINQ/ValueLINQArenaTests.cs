using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        #region Milestone 2: ValueLINQArena Value Equality, Hashing, Collections, and Telemetry (R2)

        [Fact]
        public void ValueLINQArenaImplementaIEquatableYEsBlittableDe8Bytes()
        {
            // 1. Verificar que implementa IEquatable<ValueLINQArena>
            typeof(IEquatable<ValueLINQArena>).IsAssignableFrom(typeof(ValueLINQArena)).Should().BeTrue();

            // 2. Verificar que implementa IDisposable
            typeof(IDisposable).IsAssignableFrom(typeof(ValueLINQArena)).Should().BeTrue();

            // 3. Invariante de tipo de valor y tamaño exacto en memoria (blittable, 8 bytes = long TokenArena)
            typeof(ValueLINQArena).IsValueType.Should().BeTrue();
            Unsafe.SizeOf<ValueLINQArena>().Should().Be(8);
        }

        [Fact]
        public void ValueLINQArenaIdEsPropiedadPublicaYReflejaTokenHelper()
        {
            // 1. Refutación de visibilidad: Id debe ser public (no internal)
            PropertyInfo? prop = typeof(ValueLINQArena).GetProperty("Id");
            prop.Should().NotBeNull();
            prop!.GetMethod.Should().NotBeNull();
            prop.GetMethod!.IsPublic.Should().BeTrue("el hito 2 exige exponer public int Id para observabilidad y telemetría");

            // 2. Comprobación de valor con arena viva
            using ValueLINQArena arena = ValueLINQArena.Crear();
            int idCalculado = TokenHelper.ObtenerIdTokenArena(arena.TokenArena);
            arena.Id.Should().Be(idCalculado);
            arena.Id.Should().BeGreaterThan(0);

            // 3. Comprobación de valor con default(ValueLINQArena)
            ValueLINQArena defaultArena = default;
            defaultArena.Id.Should().Be(0);
        }

        [Fact]
        public void IEquatableCumpleRelacionDeEquivalenciaReflexivaSimetricaYTransitiva()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena aCopia2 = a;
            ValueLINQArena defaultArena = default;

            ValueLINQArena dispuesta = ValueLINQArena.Crear();
            dispuesta.Dispose();
            ValueLINQArena dispuestaCopia = dispuesta;

            // Reflexividad: x.Equals(x) es siempre true
            a.Equals(a).Should().BeTrue();
            b.Equals(b).Should().BeTrue();
            defaultArena.Equals(defaultArena).Should().BeTrue();
            dispuesta.Equals(dispuesta).Should().BeTrue();

            // Simetría: x.Equals(y) == y.Equals(x)
            a.Equals(aCopia).Should().BeTrue();
            aCopia.Equals(a).Should().BeTrue();
            a.Equals(b).Should().BeFalse();
            b.Equals(a).Should().BeFalse();
            a.Equals(defaultArena).Should().BeFalse();
            defaultArena.Equals(a).Should().BeFalse();
            dispuesta.Equals(dispuestaCopia).Should().BeTrue();
            dispuestaCopia.Equals(dispuesta).Should().BeTrue();
            dispuesta.Equals(a).Should().BeFalse();
            a.Equals(dispuesta).Should().BeFalse();

            // Transitividad: si x.Equals(y) y y.Equals(z) => x.Equals(z)
            a.Equals(aCopia).Should().BeTrue();
            aCopia.Equals(aCopia2).Should().BeTrue();
            a.Equals(aCopia2).Should().BeTrue();

            // Despacho a través de la interfaz IEquatable<ValueLINQArena>
            ((IEquatable<ValueLINQArena>)a).Equals(aCopia).Should().BeTrue();
            ((IEquatable<ValueLINQArena>)a).Equals(b).Should().BeFalse();
            ((IEquatable<ValueLINQArena>)a).Equals(defaultArena).Should().BeFalse();
        }

        [Fact]
        public void OperadoresIgualdadComportamientoConIdenticosDistintosYDefault()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena default1 = default;
            ValueLINQArena default2 = default;

            // 1. Mismas instancias / copias idénticas
            (a == aCopia).Should().BeTrue();
            (a != aCopia).Should().BeFalse();

            // 2. Arenas distintas
            (a == b).Should().BeFalse();
            (a != b).Should().BeTrue();

            // 3. Default vs Default
            (default1 == default2).Should().BeTrue();
            (default1 != default2).Should().BeFalse();

            // 4. Default vs Arena activa
            (a == default1).Should().BeFalse();
            (default1 == a).Should().BeFalse();
            (a != default1).Should().BeTrue();
            (default1 != a).Should().BeTrue();

            // 5. Invariante booleana estricta: (x == y) == !(x != y)
            (a == b).Should().Be(!(a != b));
            (a == aCopia).Should().Be(!(a != aCopia));
            (default1 == default2).Should().Be(!(default1 != default2));
            (default1 == a).Should().Be(!(default1 != a));
        }

        [Fact]
        public void OperadoresIgualdadArenaDispuestaPreservaIgualdadPorValor()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQArena copia = arena;

            // Antes de disponer
            arena.IsViva.Should().BeTrue();
            (arena == copia).Should().BeTrue();
            arena.Equals(copia).Should().BeTrue();

            // Disponer la arena cambia su estado operativo pero no su identidad de token por valor
            arena.Dispose();
            arena.IsViva.Should().BeFalse();
            copia.IsViva.Should().BeFalse();

            (arena == copia).Should().BeTrue();
            (arena != copia).Should().BeFalse();
            arena.Equals(copia).Should().BeTrue();
            copia.Equals(arena).Should().BeTrue();
            arena.GetHashCode().Should().Be(copia.GetHashCode());
        }

        [Fact]
        public void AdversarialArenasConMismoIdYDistintaGeneracionNoSonIguales()
        {
            // 1. Crear Arena A en ranura S con generación G
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            int slotS = arenaA.Id;
            long genA = TokenHelper.ObtenerGeneracionTokenArena(arenaA.TokenArena);
            arenaA.Dispose();

            // 2. Reciclar la ranura S para obtener Arena B con generación G + 1
            List<ValueLINQArena> intermedias = [];
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

                long genB = TokenHelper.ObtenerGeneracionTokenArena(arenaB.TokenArena);
                arenaB.Id.Should().Be(slotS);
                genB.Should().Be(genA + 1);

                // 3. REFUTACIÓN ADVERSARIAL: Aunque compartan Id, NO son iguales
                (arenaA == arenaB).Should().BeFalse("dos arenas con distinta generación en el mismo slot jamás deben ser iguales");
                (arenaA != arenaB).Should().BeTrue();
                arenaA.Equals(arenaB).Should().BeFalse();
                arenaB.Equals(arenaA).Should().BeFalse();
                arenaA.Equals((object)arenaB).Should().BeFalse();

                // 4. HashCode debe ser diferente dado el shift generacional
                arenaA.GetHashCode().Should().NotBe(arenaB.GetHashCode());
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

        [Fact]
        public void EqualsObjectMismoTipoDistintoTipoYNulo()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena defaultArena = default;

            // 1. Comparación contra null (nunca debe lanzar excepción)
            a.Equals(null).Should().BeFalse();
            defaultArena.Equals(null).Should().BeFalse();

            // 2. Mismo tipo dentro de object (boxing)
            object boxedA = a;
            object boxedACopia = aCopia;
            object boxedB = b;
            object boxedDefault = defaultArena;

            a.Equals(boxedA).Should().BeTrue();
            a.Equals(boxedACopia).Should().BeTrue();
            a.Equals(boxedB).Should().BeFalse();
            a.Equals(boxedDefault).Should().BeFalse();

            defaultArena.Equals(boxedDefault).Should().BeTrue();
            defaultArena.Equals(boxedA).Should().BeFalse();

            // 3. Tipos completamente ajenos (adversarial probes de falso positivo)
            a.Equals(a.Id).Should().BeFalse("un int de slot no debe igualar a una ValueLINQArena");
            a.Equals(a.TokenArena).Should().BeFalse("un long de token en bruto no debe igualar a una ValueLINQArena");
            a.Equals("ValueLINQArena").Should().BeFalse();
            a.Equals(DateTime.UtcNow).Should().BeFalse();
            a.Equals(new object()).Should().BeFalse();
            defaultArena.Equals(0).Should().BeFalse();
            defaultArena.Equals(0L).Should().BeFalse();
        }

        [Fact]
        public void GetHashCodeConsistenteConIgualdadYEstable()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena defaultArena = default;

            // 1. Contrato canónico: a == b => a.GetHashCode() == b.GetHashCode()
            (a == aCopia).Should().BeTrue();
            a.GetHashCode().Should().Be(aCopia.GetHashCode());

            // 2. default(ValueLINQArena) tiene hash determinista igual a 0L.GetHashCode()
            defaultArena.GetHashCode().Should().Be(0L.GetHashCode());

            // 3. Invarianza temporal / idempotencia tras llamadas repetidas y Dispose
            int hash1 = a.GetHashCode();
            int hash2 = a.GetHashCode();
            hash1.Should().Be(hash2);

            a.Dispose();
            int hashPostDispose = a.GetHashCode();
            hashPostDispose.Should().Be(hash1);

            // 4. Distribución de hashes: 32 arenas vivas simultáneas tienen hashes únicos
            List<ValueLINQArena> arenas = [];
            HashSet<int> hashes = [];
            try
            {
                for (int i = 0; i < 32; i++)
                {
                    ValueLINQArena nueva = ValueLINQArena.Crear();
                    arenas.Add(nueva);
                    _ = hashes.Add(nueva.GetHashCode());
                }

                hashes.Count.Should().Be(32, "cada arena con id/generación diferente debe producir un hash distinto sin colisiones");
            }
            finally
            {
                foreach (ValueLINQArena arena in arenas)
                    if (arena.IsViva)
                        arena.Dispose();
            }
        }

        [Fact]
        public void ColeccionesHashSetYDictionaryOperanCorrectamenteConArenasComoClaves()
        {
            using ValueLINQArena a1 = ValueLINQArena.Crear();
            using ValueLINQArena a2 = ValueLINQArena.Crear();
            ValueLINQArena a1Copia = a1;
            ValueLINQArena def = default;

            // 1. HashSet<ValueLINQArena>
            HashSet<ValueLINQArena> set = [];
            set.Add(a1).Should().BeTrue();
            set.Add(a1).Should().BeFalse("no debe duplicar la misma instancia");
            set.Add(a1Copia).Should().BeFalse("no debe duplicar una copia por valor");
            set.Add(a2).Should().BeTrue();
            set.Add(def).Should().BeTrue("default(ValueLINQArena) es una clave válida");
            set.Add(def).Should().BeFalse();

            set.Count.Should().Be(3);
            set.Contains(a1).Should().BeTrue();
            set.Contains(a1Copia).Should().BeTrue();
            set.Contains(a2).Should().BeTrue();
            set.Contains(def).Should().BeTrue();

            // 2. Dictionary<ValueLINQArena, int>
            Dictionary<ValueLINQArena, int> dict = [];
            dict[a1] = 100;
            dict[a2] = 200;
            dict[def] = 0;

            dict.Count.Should().Be(3);
            dict[a1].Should().Be(100);
            dict[a1Copia].Should().Be(100);
            dict[a2].Should().Be(200);
            dict[def].Should().Be(0);

            // Actualización de clave por valor
            dict[a1Copia] = 999;
            dict[a1].Should().Be(999);

            dict.TryGetValue(a1Copia, out int valor).Should().BeTrue();
            valor.Should().Be(999);
        }

        [Fact]
        public void AdversarialHashSetDiferenciaArenasConMismoIdYDistintaGeneracion()
        {
            // Crear Arena A
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            int slotS = arenaA.Id;
            arenaA.Dispose();

            // Reciclar slotS para Arena B
            List<ValueLINQArena> intermedias = [];
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

                arenaA.Id.Should().Be(arenaB.Id);

                // Almacenar en HashSet
                HashSet<ValueLINQArena> set = [];
                set.Add(arenaA).Should().BeTrue();
                set.Add(arenaB).Should().BeTrue("Arena B tiene distinta generación y no debe colisionar con Arena A en el set");
                set.Count.Should().Be(2);
                set.Contains(arenaA).Should().BeTrue();
                set.Contains(arenaB).Should().BeTrue();

                // Almacenar en Dictionary
                Dictionary<ValueLINQArena, int> dict = [];
                dict[arenaA] = 1;
                dict[arenaB] = 2;

                dict.Count.Should().Be(2);
                dict[arenaA].Should().Be(1);
                dict[arenaB].Should().Be(2);
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

        [Fact]
        public void ZeroAllocationEqualityComparerDefaultNoGeneraBoxing()
        {
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();
            EqualityComparer<ValueLINQArena> comparer = EqualityComparer<ValueLINQArena>.Default;

            // Calentamiento previo para estabilizar JIT y estructuras internas
            _ = comparer.Equals(arenaA, arenaB);
            _ = comparer.GetHashCode(arenaA);

            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                _ = comparer.Equals(arenaA, arenaB);
                _ = comparer.GetHashCode(arenaA);
            }

            long memoriaDespues = GC.GetAllocatedBytesForCurrentThread();
            long asignaciones = memoriaDespues - memoriaAntes;

            asignaciones.Should().Be(0, "IEquatable<ValueLINQArena> garantiza que GenericEqualityComparer no realiza boxing");
        }

        [Fact]
        public void AdversarialReciclajeMultiGeneracionalMismoSlotHashSetYDictionaryAislados()
        {
            // Probar 10 generaciones sucesivas del MISMO slot S.
            // Cada generación debe:
            // 1. Tener arena.Id == S exactamente.
            // 2. No ser igual (==, Equals) a NINGUNA otra generación de la lista.
            // 3. Coexistir pacíficamente en HashSet<ValueLINQArena> sin colisionar ni sobreescribir.
            // 4. Coexistir en Dictionary<ValueLINQArena, int> con valores independientes.
            const int totalGeneraciones = 10;
            List<ValueLINQArena> arenasMismoSlot = [];
            List<ValueLINQArena> intermedias = [];

            try
            {
                // Alquilar una arena inicial para capturar el slot objetivo
                ValueLINQArena primera = ValueLINQArena.Crear();
                int slotObjetivo = primera.Id;
                long generacionInicial = TokenHelper.ObtenerGeneracionTokenArena(primera.TokenArena);
                primera.Dispose();
                arenasMismoSlot.Add(primera);

                for (int gen = 1; gen < totalGeneraciones; gen++)
                {
                    while (true)
                    {
                        ValueLINQArena candidata = ValueLINQArena.Crear();
                        if (candidata.Id == slotObjetivo)
                        {
                            candidata.Dispose();
                            arenasMismoSlot.Add(candidata);
                            break;
                        }
                        intermedias.Add(candidata);
                    }
                }

                arenasMismoSlot.Count.Should().Be(totalGeneraciones);

                // 1. Verificar que todas comparten slotObjetivo pero tienen generaciones estrictamente crecientes
                for (int i = 0; i < arenasMismoSlot.Count; i++)
                {
                    ValueLINQArena a = arenasMismoSlot[i];
                    a.Id.Should().Be(slotObjetivo);
                    long gen = TokenHelper.ObtenerGeneracionTokenArena(a.TokenArena);
                    gen.Should().Be(generacionInicial + i);
                }

                // 2. Falsificación por pares: Ninguna arena debe ser == o .Equals() a otra
                for (int i = 0; i < arenasMismoSlot.Count; i++)
                {
                    for (int j = 0; j < arenasMismoSlot.Count; j++)
                    {
                        if (i == j)
                        {
                            (arenasMismoSlot[i] == arenasMismoSlot[j]).Should().BeTrue();
                            arenasMismoSlot[i].Equals(arenasMismoSlot[j]).Should().BeTrue();
                            arenasMismoSlot[i].Equals((object)arenasMismoSlot[j]).Should().BeTrue();
                        }
                        else
                        {
                            (arenasMismoSlot[i] == arenasMismoSlot[j]).Should().BeFalse();
                            (arenasMismoSlot[i] != arenasMismoSlot[j]).Should().BeTrue();
                            arenasMismoSlot[i].Equals(arenasMismoSlot[j]).Should().BeFalse();
                            arenasMismoSlot[i].Equals((object)arenasMismoSlot[j]).Should().BeFalse();
                            EqualityComparer<ValueLINQArena>.Default.Equals(arenasMismoSlot[i], arenasMismoSlot[j]).Should().BeFalse();
                        }
                    }
                }

                // 3. Prueba en HashSet<ValueLINQArena>
                HashSet<ValueLINQArena> set = [];
                foreach (ValueLINQArena a in arenasMismoSlot)
                    set.Add(a).Should().BeTrue("cada generación del mismo slot debe insertarse de forma única");

                set.Count.Should().Be(totalGeneraciones);
                foreach (ValueLINQArena a in arenasMismoSlot)
                    set.Contains(a).Should().BeTrue();

                // 4. Prueba en Dictionary<ValueLINQArena, int>
                Dictionary<ValueLINQArena, int> dict = [];
                for (int i = 0; i < arenasMismoSlot.Count; i++)
                    dict[arenasMismoSlot[i]] = (i + 1) * 100;

                dict.Count.Should().Be(totalGeneraciones);

                for (int i = 0; i < arenasMismoSlot.Count; i++)
                {
                    dict.TryGetValue(arenasMismoSlot[i], out int valor).Should().BeTrue();
                    valor.Should().Be((i + 1) * 100);
                }

                // Mutación selectiva: modificar una clave no debe mutar las otras generaciones del mismo slot
                dict[arenasMismoSlot[0]] = 9999;
                dict[arenasMismoSlot[0]].Should().Be(9999);
                for (int i = 1; i < arenasMismoSlot.Count; i++)
                    dict[arenasMismoSlot[i]].Should().Be((i + 1) * 100);
            }
            finally
            {
                foreach (ValueLINQArena intermedia in intermedias)
                    if (intermedia.IsViva)
                        intermedia.Dispose();
            }
        }

        [Fact]
        public void AdversarialDefaultArenaEnHashSetYDictionaryComportamientoExhaustivo()
        {
            ValueLINQArena def1 = default;
            ValueLINQArena def2 = default;

            using ValueLINQArena viva = ValueLINQArena.Crear();
            ValueLINQArena dispuesta = ValueLINQArena.Crear();
            dispuesta.Dispose();

            // 1. Propiedades intrínsecas de default(ValueLINQArena)
            def1.Id.Should().Be(0);
            def1.IsViva.Should().BeFalse();
            def1.GetHashCode().Should().Be(0);
            def1.Equals(def2).Should().BeTrue();
            (def1 == def2).Should().BeTrue();
            (def1 != def2).Should().BeFalse();
            def1.Equals(null).Should().BeFalse();
            def1.Equals(new object()).Should().BeFalse();
            def1.Equals(0).Should().BeFalse();
            def1.Equals(0L).Should().BeFalse();

            // Despacho a través de object (unboxing)
            object boxedDef = def1;
            def2.Equals(boxedDef).Should().BeTrue();
            boxedDef.Equals(def2).Should().BeTrue();
            boxedDef.Equals(viva).Should().BeFalse();

            // No igualdad contra arenas vivas o dispuestas reales
            def1.Equals(viva).Should().BeFalse();
            (def1 == viva).Should().BeFalse();
            def1.Equals(dispuesta).Should().BeFalse();
            (def1 == dispuesta).Should().BeFalse();

            // 2. HashSet<ValueLINQArena> exhaustivo
            HashSet<ValueLINQArena> set = [];
            set.Add(def1).Should().BeTrue();
            set.Add(def2).Should().BeFalse("duplicado de default");
            set.Count.Should().Be(1);
            set.Contains(default).Should().BeTrue();
            set.Contains(viva).Should().BeFalse();

            set.Add(viva).Should().BeTrue();
            set.Add(dispuesta).Should().BeTrue();
            set.Count.Should().Be(3);

            set.Remove(def1).Should().BeTrue();
            set.Count.Should().Be(2);
            set.Contains(default).Should().BeFalse();
            set.Contains(viva).Should().BeTrue();
            set.Contains(dispuesta).Should().BeTrue();

            // 3. Dictionary<ValueLINQArena, string> exhaustivo
            Dictionary<ValueLINQArena, string> dict = [];
            dict[def1] = "valor_default";
            dict.ContainsKey(default).Should().BeTrue();
            dict.ContainsKey(viva).Should().BeFalse();
            dict[def1].Should().Be("valor_default");
            dict[def2].Should().Be("valor_default");

            dict[viva] = "valor_viva";
            dict[dispuesta] = "valor_dispuesta";
            dict.Count.Should().Be(3);

            dict.TryGetValue(default, out string? vDef).Should().BeTrue();
            vDef.Should().Be("valor_default");

            dict.TryGetValue(viva, out string? vViva).Should().BeTrue();
            vViva.Should().Be("valor_viva");

            // Mutar default
            dict[def2] = "default_modificado";
            dict[def1].Should().Be("default_modificado");
            dict[viva].Should().Be("valor_viva");

            dict.Remove(default).Should().BeTrue();
            dict.Count.Should().Be(2);
            dict.ContainsKey(default).Should().BeFalse();
        }

        [Fact]
        public void AdversarialArenaIdPublicoPreservaSlotIdYEqualsDistingueGeneraciones()
        {
            // Verificar en múltiples arenas activas y recicladas que Id siempre reporta el slot físico (1..4095)
            // mientras que Equals distingue unívocamente tokens de diferentes generaciones.
            List<ValueLINQArena> activas = [];
            try
            {
                for (int i = 0; i < 16; i++)
                    activas.Add(ValueLINQArena.Crear());

                foreach (ValueLINQArena arena in activas)
                {
                    arena.Id.Should().BeInRange(1, ValueLINQConfig.Arenas - 1);
                    int idToken = TokenHelper.ObtenerIdTokenArena(arena.TokenArena);
                    arena.Id.Should().Be(idToken);
                }

                // Crear y reciclar una arena específica
                ValueLINQArena a1 = ValueLINQArena.Crear();
                int slot = a1.Id;
                a1.Dispose();

                List<ValueLINQArena> drenaje = [];
                ValueLINQArena a2 = default;
                try
                {
                    while (true)
                    {
                        ValueLINQArena c = ValueLINQArena.Crear();
                        if (c.Id == slot)
                        {
                            a2 = c;
                            break;
                        }
                        drenaje.Add(c);
                    }

                    // A1 y A2 tienen el mismo Id físico
                    a1.Id.Should().Be(slot);
                    a2.Id.Should().Be(slot);
                    (a1.Id == a2.Id).Should().BeTrue();

                    // Pero Equals y == jamás deben coincidir
                    (a1 == a2).Should().BeFalse();
                    (a1 != a2).Should().BeTrue();
                    a1.Equals(a2).Should().BeFalse();
                    a2.Equals(a1).Should().BeFalse();

                    // El hash code tampoco debe coincidir dado que los tokens son diferentes
                    a1.GetHashCode().Should().NotBe(a2.GetHashCode());
                }
                finally
                {
                    if (a2.IsViva)
                        a2.Dispose();

                    foreach (ValueLINQArena d in drenaje)
                        if (d.IsViva)
                            d.Dispose();
                }
            }
            finally
            {
                foreach (ValueLINQArena a in activas)
                    if (a.IsViva)
                        a.Dispose();
            }
        }

        [Fact]
        public void AdversarialZeroAllocationDirectEqualsYOperadores()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena def = default;
            ValueLINQArena def2 = default;

            // Warm up JIT
            bool res = a.Equals(b) || (a == b) || (a != b) || (a == aCopia) || (def == def2);

            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000_000; i++)
            {
                res &= a.Equals(aCopia);
                res &= (a == aCopia);
                res &= !(a == b);
                res &= (a != b);
                res &= !(a == def);
                res &= (def == def2);
            }

            long memoriaDespues = GC.GetAllocatedBytesForCurrentThread();
            long asignado = memoriaDespues - memoriaAntes;

            asignado.Should().Be(0, "Equals por valor y operadores ==/!= no deben realizar asignación heap alguna");
            res.Should().BeTrue();
        }

        [Fact]
        public void AdversarialZeroAllocationGenericConstrainedEquals()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;

            // Warm up
            _ = CompararGenerico(a, aCopia);

            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();

            bool res = true;
            for (int i = 0; i < 1_000_000; i++)
                res &= CompararGenerico(a, aCopia);

            long memoriaDespues = GC.GetAllocatedBytesForCurrentThread();
            long asignado = memoriaDespues - memoriaAntes;

            asignado.Should().Be(0, "Llamar a IEquatable<T>.Equals a través de un método con restricción genérica no debe boxing en RyuJIT");
            res.Should().BeTrue();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static bool CompararGenerico<T>(T x, T y) where T : IEquatable<T> => x.Equals(y);
        }

        [Fact]
        public void AdversarialEqualityComparerTipoRuntimeYZeroAllocation()
        {
            EqualityComparer<ValueLINQArena> comparer = EqualityComparer<ValueLINQArena>.Default;

            // Refutación del tipo de comparer: debe ser GenericEqualityComparer, no ObjectEqualityComparer
            string tipoNombre = comparer.GetType().Name;
            tipoNombre.Should().Contain("GenericEqualityComparer", "al implementar IEquatable<ValueLINQArena>, el BCL debe resolver GenericEqualityComparer<T> para evitar boxing");

            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();

            // Warm up loop to trigger Tier-1 JIT / OSR prior to measurement
            for (int i = 0; i < 50_000; i++)
            {
                _ = comparer.Equals(a, b);
                _ = comparer.GetHashCode(a);
            }

            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000_000; i++)
            {
                _ = comparer.Equals(a, b);
                _ = comparer.GetHashCode(a);
            }

            long memoriaDespues = GC.GetAllocatedBytesForCurrentThread();
            long asignado = memoriaDespues - memoriaAntes;

            asignado.Should().Be(0, "EqualityComparer<ValueLINQArena>.Default no debe asignar en heap");
        }

        [Fact]
        public void AdversarialEqualsObjectTiposExhaustivosYSinExcepciones()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQArena def = default;

            // 1. Probar que no lanza excepciones con entradas dispares
            Action actNull = () => _ = arena.Equals((object?)null);
            actNull.Should().NotThrow();

            Action actString = () => _ = arena.Equals("JCarrillo.AOT.Core");
            actString.Should().NotThrow();

            Action actInt = () => _ = arena.Equals(12345);
            actInt.Should().NotThrow();

            Action actLong = () => _ = arena.Equals(arena.TokenArena);
            actLong.Should().NotThrow();

            Action actObj = () => _ = arena.Equals(new object());
            actObj.Should().NotThrow();

            Action actGuid = () => _ = arena.Equals(Guid.NewGuid());
            actGuid.Should().NotThrow();

            Action actArr = () => _ = arena.Equals(new byte[] { 1, 2, 3 });
            actArr.Should().NotThrow();

            // 2. Falsificación: arena.Equals(arena.TokenArena) DEBE ser false (un long no es una ValueLINQArena)
            arena.Equals((object)arena.TokenArena).Should().BeFalse();
            def.Equals((object)0L).Should().BeFalse();

            // 3. Probar que Equals(object?) con instancia ya empaquetada no genera asignaciones secundarias
            object boxed = arena;
            _ = arena.Equals(boxed); // Calentamiento

            long memoriaAntes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1_000_000; i++)
                _ = arena.Equals(boxed);

            long memoriaDespues = GC.GetAllocatedBytesForCurrentThread();
            long asignado = memoriaDespues - memoriaAntes;

            asignado.Should().Be(0, "Equals(object?) no debe crear asignaciones heap secundarias al desempaquetar");
        }

        [Fact]
        public void AdversarialGetHashCodeEstabilidadDistribucionYPersistenciaEnHashSetTrasDispose()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();

            // 1. Estabilidad a lo largo de 1,000,000 llamadas
            int hashInicial = arena.GetHashCode();
            for (int i = 0; i < 1_000_000; i++)
                arena.GetHashCode().Should().Be(hashInicial);

            // 2. Insertar en HashSet y verificar persistencia tras Dispose()
            ValueLINQArena copia = arena;
            HashSet<ValueLINQArena> set = [arena];
            set.Contains(copia).Should().BeTrue();

            // Disponer la arena
            arena.Dispose();

            // La copia (y la arena dispuesta) deben seguir teniendo el mismo HashCode
            arena.GetHashCode().Should().Be(hashInicial);
            copia.GetHashCode().Should().Be(hashInicial);

            // Deben seguir encontrándose en el HashSet sin corrupción de buckets
            set.Contains(arena).Should().BeTrue();
            set.Contains(copia).Should().BeTrue();

            // 3. Simulación de distribución de 9,600 tokens sintéticos: comprobar baja tasa de colisiones
            HashSet<int> hashes = [];
            for (int id = 1; id <= 64; id++)
            {
                for (long gen = 1; gen <= 150; gen++)
                {
                    long token = TokenHelper.CrearTokenArena(id, gen);
                    _ = hashes.Add(token.GetHashCode());
                }
            }

            // 64 * 150 = 9,600 tokens
            hashes.Count.Should().Be(9600, "9,600 combinaciones distintas de id y generación no deben presentar colisiones en GetHashCode");
        }

        [Fact]
        public void AdversarialOperadoresEquivalenciaTablaDeVerdadCompleta()
        {
            using ValueLINQArena a = ValueLINQArena.Crear();
            using ValueLINQArena b = ValueLINQArena.Crear();
            ValueLINQArena aCopia = a;
            ValueLINQArena def = default;

            ValueLINQArena[] arenas = [a, b, aCopia, def];
            EqualityComparer<ValueLINQArena> comparer = EqualityComparer<ValueLINQArena>.Default;

            foreach (ValueLINQArena x in arenas)
            {
                foreach (ValueLINQArena y in arenas)
                {
                    bool opEq = x == y;
                    bool opNeq = x != y;
                    bool eqMethod = x.Equals(y);
                    bool objEqMethod = x.Equals((object)y);
                    bool compEq = comparer.Equals(x, y);

                    opEq.Should().Be(!opNeq, "x == y debe ser estrictamente !(x != y)");
                    opEq.Should().Be(eqMethod, "x == y debe coincidir con x.Equals(y)");
                    opEq.Should().Be(objEqMethod, "x == y debe coincidir con x.Equals((object)y)");
                    opEq.Should().Be(compEq, "x == y debe coincidir con EqualityComparer<ValueLINQArena>.Default.Equals(x, y)");

                    if (opEq)
                        x.GetHashCode().Should().Be(y.GetHashCode(), "si dos arenas son iguales, sus hash codes deben coincidir");
                }
            }
        }

        #endregion
    }
}

