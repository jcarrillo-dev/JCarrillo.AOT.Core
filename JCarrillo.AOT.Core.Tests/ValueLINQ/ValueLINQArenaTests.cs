using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
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

        #region Pruebas Empíricas Adversariales

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

        #region Igualdad por Valor, Hashing y Telemetría en ValueLINQArena

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
            prop.GetMethod!.IsPublic.Should().BeTrue("se exige exponer public int Id para observabilidad y telemetría");

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

        #region Sobrecargas de Colecciones con Arena y Alias de Compatibilidad

        private readonly struct TestChunkCounter(int[] counter) : IProcesarChunkDelegado<int>
        {
            private readonly int[] _counter = counter;

            public readonly void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                int count = 0;
                foreach (int _ in listaChunk)
                    count++;
                _counter[0] += count;
            }
        }

        private struct TestChunkThrowing : IProcesarChunkDelegado<int>
        {
            public readonly void Ejecutar(ValueLINQStruct<int> listaChunk)
            {
                foreach (int item in listaChunk)
                    if (item == 3)
                        throw new InvalidOperationException("Simulated error in chunk processor");
            }
        }

        #region Sobrecargas con Arena Activa

        [Fact]
        public void SpanToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] buffer = [1, 2, 3, 4, 5];
            Span<int> span = buffer.AsSpan(0, buffer.Length);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using ValueLINQStruct<int> query = span.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            using ValueLINQStruct<int> filtrado = query.Where(2, new PredicadoMayorQue());
            _ = filtrado.IsValido.Should().BeTrue();
            _ = filtrado.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in filtrado)
                suma += x;

            _ = suma.Should().Be(3 + 4 + 5);
        }

        [Fact]
        public void SpanToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] buffer = [1, 2, 3, 4, 5];
            Span<int> span = buffer.AsSpan(0, buffer.Length);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQRefStruct<int> query = span.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            ValueLINQRefStruct<int> filtrado = query.Where(2, new PredicadoMayorQue());
            _ = filtrado.IsValido.Should().BeTrue();
            _ = filtrado.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (ref int x in filtrado)
                suma += x;

            _ = suma.Should().Be(3 + 4 + 5);
        }

        [Fact]
        public void ReadOnlySpanToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            ReadOnlySpan<int> roSpan = [10, 20, 30, 40];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using ValueLINQStruct<int> query = roSpan.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in query)
                suma += x;

            _ = suma.Should().Be(10 + 20 + 30 + 40);
        }

        [Fact]
        public void ReadOnlySpanToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            ReadOnlySpan<int> roSpan = [10, 20, 30, 40];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQRefStruct<int> query = roSpan.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (ref int x in query)
                suma += x;

            _ = suma.Should().Be(10 + 20 + 30 + 40);
        }

        [Fact]
        public void MemoryToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] buffer = [100, 200, 300];
            Memory<int> memory = new(buffer);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            Memory<int> localMemory = memory;
            using ValueLINQStruct<int> query = localMemory.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in query)
                suma += x;

            _ = suma.Should().Be(600);
        }

        [Fact]
        public void MemoryToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            int[] buffer = [100, 200, 300];
            Memory<int> memory = new(buffer);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            Memory<int> localMemory = memory;
            ValueLINQRefStruct<int> query = localMemory.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (ref int x in query)
                suma += x;

            _ = suma.Should().Be(600);
        }

        [Fact]
        public void PooledListToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            using PooledList<int> list = new();
            list.AddRange([5, 10, 15, 20]);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            PooledList<int> localList = list;
            using ValueLINQStruct<int> query = localList.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in query)
                suma += x;

            _ = suma.Should().Be(50);
        }

        [Fact]
        public void PooledListToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            using PooledList<int> list = new();
            list.AddRange([5, 10, 15, 20]);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            PooledList<int> localList = list;
            ValueLINQRefStruct<int> query = localList.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (ref int x in query)
                suma += x;

            _ = suma.Should().Be(50);
        }

        [Fact]
        public void PooledArrayToValueQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            using PooledArray<int> array = new(4);
            array.Span[0] = 7;
            array.Span[1] = 14;
            array.Span[2] = 21;
            array.Span[3] = 28;
            using ValueLINQArena arena = ValueLINQArena.Crear();

            PooledArray<int> localArray = array;
            using ValueLINQStruct<int> query = localArray.ToValueQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (int x in query)
                suma += x;

            _ = suma.Should().Be(70);
        }

        [Fact]
        public void PooledArrayToValueRefQueryConArenaVivaRutaFelizYPropagacionTokenArena()
        {
            using PooledArray<int> array = new(4);
            array.Span[0] = 7;
            array.Span[1] = 14;
            array.Span[2] = 21;
            array.Span[3] = 28;
            using ValueLINQArena arena = ValueLINQArena.Crear();

            PooledArray<int> localArray = array;
            ValueLINQRefStruct<int> query = localArray.ToValueRefQuery(arena);

            _ = query.IsValido.Should().BeTrue();
            _ = query.TokenArena.Should().Be(arena.TokenArena);

            int suma = 0;
            foreach (ref int x in query)
                suma += x;

            _ = suma.Should().Be(70);
        }

        #endregion

        #region Sobrecargas con Arena Default

        [Fact]
        public void SpanToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Span<int> span = buffer.AsSpan(0, buffer.Length);

            try
            {
                _ = span.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void SpanToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Span<int> span = buffer.AsSpan(0, buffer.Length);

            try
            {
                _ = span.ToValueRefQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void ReadOnlySpanToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            ReadOnlySpan<int> roSpan = [1, 2, 3];

            try
            {
                _ = roSpan.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void ReadOnlySpanToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            ReadOnlySpan<int> roSpan = [1, 2, 3];

            try
            {
                _ = roSpan.ToValueRefQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void MemoryToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Memory<int> memory = new(buffer);
            Memory<int> localMemory = memory;

            try
            {
                _ = localMemory.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void MemoryToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Memory<int> memory = new(buffer);
            Memory<int> localMemory = memory;

            try
            {
                _ = localMemory.ToValueRefQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void PooledListToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            using PooledList<int> list = new();
            list.AddRange([1, 2, 3]);
            PooledList<int> localList = list;

            try
            {
                _ = localList.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void PooledListToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            using PooledList<int> list = new();
            list.AddRange([1, 2, 3]);
            PooledList<int> localList = list;

            try
            {
                _ = localList.ToValueRefQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void PooledArrayToValueQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            using PooledArray<int> array = new(3);
            PooledArray<int> localArray = array;

            try
            {
                _ = localArray.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void PooledArrayToValueRefQueryConDefaultArenaLanzaValueLinqArenaInactivaException()
        {
            using PooledArray<int> array = new(3);
            PooledArray<int> localArray = array;

            try
            {
                _ = localArray.ToValueRefQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        #endregion

        #region Sobrecargas con Arena Dispuesta

        [Fact]
        public void SpanToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Span<int> span = buffer.AsSpan(0, buffer.Length);
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = span.ToValueQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void SpanToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Span<int> span = buffer.AsSpan(0, buffer.Length);
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = span.ToValueRefQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void ReadOnlySpanToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            ReadOnlySpan<int> roSpan = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = roSpan.ToValueQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void ReadOnlySpanToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            ReadOnlySpan<int> roSpan = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = roSpan.ToValueRefQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void MemoryToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Memory<int> memory = new(buffer);
            Memory<int> localMemory = memory;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localMemory.ToValueQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void MemoryToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            int[] buffer = [1, 2, 3];
            Memory<int> memory = new(buffer);
            Memory<int> localMemory = memory;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localMemory.ToValueRefQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void PooledListToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            using PooledList<int> list = new();
            list.AddRange([1, 2, 3]);
            PooledList<int> localList = list;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localList.ToValueQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void PooledListToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            using PooledList<int> list = new();
            list.AddRange([1, 2, 3]);
            PooledList<int> localList = list;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localList.ToValueRefQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void PooledArrayToValueQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            using PooledArray<int> array = new(3);
            PooledArray<int> localArray = array;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localArray.ToValueQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        [Fact]
        public void PooledArrayToValueRefQueryConArenaDispuestaLanzaValueLinqArenaInactivaException()
        {
            using PooledArray<int> array = new(3);
            PooledArray<int> localArray = array;
            ValueLINQArena arena = ValueLINQArena.Crear();
            int idEsperado = arena.Id;
            arena.Dispose();

            try
            {
                _ = localArray.ToValueRefQuery(arena);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(idEsperado);
            }
        }

        #endregion

        #region Alias Retrocompatibles para ProcessChunks

        [Fact]
        public void ProcessChunksRefStructProduceComportamientoIdenticoAProcesarChunks()
        {
            int[] array = [1, 2, 3, 4, 5];

            // 1. Ejecución canónica con ProcesarChunks
            int[] canonCounter = new int[1];
            using (ValueLINQStruct<int> q1 = array.ToValueQuery())
            using (ValueLINQRefStruct<ValueLINQStruct<int>> chunks1 = q1.Chunk(2))
                chunks1.ProcesarChunks(new TestChunkCounter(canonCounter));

            // 2. Ejecución con alias retrocompatible ProcessChunks
            int[] aliasCounter = new int[1];
            using (ValueLINQStruct<int> q2 = array.ToValueQuery())
            using (ValueLINQRefStruct<ValueLINQStruct<int>> chunks2 = q2.Chunk(2))
            {
#pragma warning disable CS0618 // Type or member is obsolete
                chunks2.ProcessChunks(new TestChunkCounter(aliasCounter));
#pragma warning restore CS0618
            }

            _ = aliasCounter[0].Should().Be(5);
            _ = aliasCounter[0].Should().Be(canonCounter[0]);
        }

        [Fact]
        public void ProcessChunksStructProduceComportamientoIdenticoAProcesarChunks()
        {
            int[] array1 = [10, 20, 30];
            int[] array2 = [40, 50, 60];

            // 1. Ejecución canónica con ProcesarChunks en ValueLINQStruct
            int[] canonCounter = new int[1];
            ValueLINQStruct<int>[] structChunks1 = [array1.ToValueQuery(), array2.ToValueQuery()];
            using (ValueLINQStruct<ValueLINQStruct<int>> queryChunks1 = structChunks1.ToValueQuery())
                queryChunks1.ProcesarChunks(new TestChunkCounter(canonCounter));

            // 2. Ejecución con alias retrocompatible ProcessChunks en ValueLINQStruct
            int[] aliasCounter = new int[1];
            ValueLINQStruct<int>[] structChunks2 = [array1.ToValueQuery(), array2.ToValueQuery()];
            using (ValueLINQStruct<ValueLINQStruct<int>> queryChunks2 = structChunks2.ToValueQuery())
            {
#pragma warning disable CS0618 // Type or member is obsolete
                queryChunks2.ProcessChunks(new TestChunkCounter(aliasCounter));
#pragma warning restore CS0618
            }

            _ = aliasCounter[0].Should().Be(6);
            _ = aliasCounter[0].Should().Be(canonCounter[0]);
        }

        [Fact]
        public void ProcessChunksManejoDeExcepcionYRecuperacionDeBuffers()
        {
            int initialActive = ValueLINQConfig.TamañoTabla - ValueLINQStateManager<ValueLINQStruct<int>>.SlotsLibres;
            int[] array = [1, 2, 3, 4, 5];
            ValueLINQStruct<int> query = array.ToValueQuery();
            ValueLINQRefStruct<ValueLINQStruct<int>> chunks = query.Chunk(2);

            try
            {
#pragma warning disable CS0618 // Type or member is obsolete
                chunks.ProcessChunks(new TestChunkThrowing());
#pragma warning restore CS0618
                Assert.Fail("Debería haber lanzado InvalidOperationException");
            }
            catch (InvalidOperationException ex) when (ex.Message == "Simulated error in chunk processor")
            {
                // Excepción esperada
            }

            int finalActive = ValueLINQConfig.TamañoTabla - ValueLINQStateManager<ValueLINQStruct<int>>.SlotsLibres;
            _ = finalActive.Should().Be(initialActive, "todos los buffers alquilados de los fragmentos deben ser liberados al lanzar excepción");
        }

        [Fact]
        public void ProcessChunksMetadatosObsoleteVerificacionReflexiva()
        {
            MethodInfo[] metodos = typeof(ValueLINQExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static);
            List<MethodInfo> processChunksMethods = [];
            foreach (MethodInfo m in metodos)
                if (m.Name == "ProcessChunks")
                    processChunksMethods.Add(m);

            _ = processChunksMethods.Should().HaveCount(2, "deben existir exactamente 2 sobrecargas públicas de ProcessChunks");

            foreach (MethodInfo m in processChunksMethods)
            {
                ObsoleteAttribute? obsoleteAttr = m.GetCustomAttribute<ObsoleteAttribute>();
                _ = obsoleteAttr.Should().NotBeNull("ProcessChunks debe estar marcado con [Obsolete]");
                _ = obsoleteAttr!.Message.Should().Be("Use ProcesarChunks en su lugar");
                _ = obsoleteAttr.IsError.Should().BeFalse("ProcessChunks no debe ser tratado como error de compilación");
            }
        }

        #endregion

        #region Casos de Borde y Pruebas Adversariales

        [Fact]
        public void ColeccionesVaciasConArenaVivaGeneranConsultasValidasYTokenPreservado()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();

            // 1. Span vacío
            Span<int> emptySpan = Span<int>.Empty;
            using (ValueLINQStruct<int> qSpan = emptySpan.ToValueQuery(arena))
            {
                _ = qSpan.IsValido.Should().BeTrue();
                _ = qSpan.TokenArena.Should().Be(arena.TokenArena);
                int count = 0;
                foreach (int _ in qSpan)
                    count++;
                _ = count.Should().Be(0);
            }

            // 2. ReadOnlySpan vacío
            ReadOnlySpan<int> emptyRoSpan = ReadOnlySpan<int>.Empty;
            using (ValueLINQStruct<int> qRo = emptyRoSpan.ToValueQuery(arena))
            {
                _ = qRo.IsValido.Should().BeTrue();
                _ = qRo.TokenArena.Should().Be(arena.TokenArena);
                int count = 0;
                foreach (int _ in qRo)
                    count++;
                _ = count.Should().Be(0);
            }

            // 3. Memory vacía
            Memory<int> emptyMemory = Memory<int>.Empty;
            using (ValueLINQStruct<int> qMem = emptyMemory.ToValueQuery(arena))
            {
                _ = qMem.IsValido.Should().BeTrue();
                _ = qMem.TokenArena.Should().Be(arena.TokenArena);
                int count = 0;
                foreach (int _ in qMem)
                    count++;
                _ = count.Should().Be(0);
            }

            // 4. PooledList vacía
            using PooledList<int> emptyList = new();
            PooledList<int> localList = emptyList;
            using (ValueLINQStruct<int> qList = localList.ToValueQuery(arena))
            {
                _ = qList.IsValido.Should().BeTrue();
                _ = qList.TokenArena.Should().Be(arena.TokenArena);
                int count = 0;
                foreach (int _ in qList)
                    count++;
                _ = count.Should().Be(0);
            }
        }

        [Fact]
        public void ColeccionesVaciasConDefaultArenaLanzanExcepcionSinOmitirValidacion()
        {
            Span<int> emptySpan = Span<int>.Empty;
            try
            {
                _ = emptySpan.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException incluso con Span vacío");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }

            ReadOnlySpan<int> emptyRoSpan = ReadOnlySpan<int>.Empty;
            try
            {
                _ = emptyRoSpan.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException incluso con ReadOnlySpan vacío");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }

            Memory<int> emptyMem = Memory<int>.Empty;
            Memory<int> localMem = emptyMem;
            try
            {
                _ = localMem.ToValueQuery(default);
                Assert.Fail("Debería haber lanzado ValueLinqArenaInactivaException incluso con Memory vacía");
            }
            catch (ValueLinqArenaInactivaException ex)
            {
                _ = ex.IdArena.Should().Be(0);
            }
        }

        [Fact]
        public void ColeccionesAislamientoFisicoMutacionOrigenNoAfectaConsultaEnArena()
        {
            int[] backingArray = [100, 200, 300];
            Span<int> span = backingArray.AsSpan(0, backingArray.Length);
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using ValueLINQStruct<int> query = span.ToValueQuery(arena);

            // Mutación posterior en el buffer original
            backingArray[0] = 999;
            backingArray[1] = 888;
            backingArray[2] = 777;

            // La consulta en la arena debe mantener los valores originales copiados
            List<int> resultado = [];
            foreach (int item in query)
                resultado.Add(item);

            _ = resultado.Should().Equal([100, 200, 300]);
        }

        [Fact]
        public void PooledArrayToValueQueryYRefQuerySinArenaRutaFeliz()
        {
            using PooledArray<int> array = new(3);
            array.Span[0] = 11;
            array.Span[1] = 22;
            array.Span[2] = 33;

            PooledArray<int> localArray1 = array;
            using (ValueLINQStruct<int> qStruct = localArray1.ToValueQuery())
            {
                _ = qStruct.IsValido.Should().BeTrue();
                int suma = 0;
                foreach (int x in qStruct)
                    suma += x;
                _ = suma.Should().Be(66);
            }

            PooledArray<int> localArray2 = array;
            ValueLINQRefStruct<int> qRef = localArray2.ToValueRefQuery();
            _ = qRef.IsValido.Should().BeTrue();
            int sumaRef = 0;
            foreach (ref int x in qRef)
                sumaRef += x;
            _ = sumaRef.Should().Be(66);
        }

        #endregion

        #endregion
    }
}

