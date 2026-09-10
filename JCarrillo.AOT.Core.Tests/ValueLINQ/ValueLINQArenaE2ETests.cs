using System.Runtime.CompilerServices;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Diagnostico;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Marshalling;
#endif
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class ValueLINQArenaE2ETests
    {
        private struct MayorQue : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int umbral) => item > umbral;
        }

        private struct Doble : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        private struct ATexto : ISelectDelegado<int, string>
        {
            public readonly string Ejecutar(int item) => item.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static readonly int[] DatosArenaDispuesta = [1, 2, 3];
        private static readonly int[] DatosAmbiente = [10, 20];
        private static readonly int[] DatosConcatA = [1, 2];
        private static readonly int[] DatosConcatB = [3, 4];

        [Fact]
        public void LasExcepcionesDeValueLINQApuntanASuFichaJCE()
        {
            // Preparar y actuar: el mensaje es el único canal que alcanza a quien choca en el momento en que decide,
            // así que cada excepción debe remitir a su ficha.
            Exception cruzada = new ValueLinqArenaCruzadaException(1, 2);
            Exception inactiva = new ValueLinqArenaInactivaException(1);
            Exception expirada = new ValueLinqSesionExpiradaException(1L, 2L, 3);
            Exception tokenInvalido = new ValueLinqTokenInvalidoException(0L, 0);

            // Aserción
            _ = cruzada.Message.Should().Contain(JCEDiagnostico.JCE0001.Url);
            _ = inactiva.Message.Should().Contain(JCEDiagnostico.JCE0002.Url);
            _ = expirada.Message.Should().Contain(JCEDiagnostico.JCE0003.Url);
            _ = tokenInvalido.Message.Should().Contain(JCEDiagnostico.JCE0004.Url);
        }

        [Fact]
        public void CrearYDisponerUnaArenaEsCoherente()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            _ = arena.IsViva.Should().BeTrue();

            arena.Dispose();
            _ = arena.IsViva.Should().BeFalse();
        }

        [Fact]
        public void DisponerLaArenaLiberaLaConsultaCreadaEnElla()
        {
            int[] datos = [1, 2, 3, 4, 5];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<int> query = datos.ToValueQuery(arena);
            _ = query.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = query.IsValido.Should().BeFalse("al disponer la arena, su sesión debe liberarse");
        }

        [Fact]
        public void DisponerLaArenaLiberaElDestinoDeWhereAunqueSeOlvide()
        {
            int[] datos = [1, 2, 3, 4, 5, 6];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<int> filtrado = datos.ToValueQuery(arena).Where(3, new MayorQue());
            _ = filtrado.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = filtrado.IsValido.Should().BeFalse("el destino de Where debe vivir en la arena del origen y liberarse con ella");
        }

        [Fact]
        public void DisponerLaArenaLiberaElResultadoDeSelectAunqueCambieDeTipo()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<string> proyectado = datos.ToValueQuery(arena).Select<int, ATexto, string>(new ATexto());
            _ = proyectado.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = proyectado.IsValido.Should().BeFalse("la tabla del tipo string en esta arena también debe liberarse en bloque");
        }

        [Fact]
        public void UnaCadenaEnArenaProduceElResultadoCorrecto()
        {
            int[] datos = [1, 2, 3, 4, 5, 6];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            List<int> resultado = [];
            foreach (int x in datos.ToValueQuery(arena).Where(3, new MayorQue()).Select<int, Doble, int>(new Doble()))
                resultado.Add(x);

            _ = resultado.Should().Equal(8, 10, 12);
        }

        [Fact]
        public void DisponerUnaArenaNoAfectaLaArenaAmbiente()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            _ = DatosArenaDispuesta.ToValueQuery(arena);
            arena.Dispose();

            using PooledArray<int> resultado = DatosAmbiente.ToValueQuery().ToArray();

            _ = resultado.Tamaño.Should().Be(2);
            _ = resultado.Span[0].Should().Be(10);
            _ = resultado.Span[1].Should().Be(20);
        }

        [Fact]
        public void ConcatDentroDeLaMismaArenaFunciona()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQStruct<int> a = DatosConcatA.ToValueQuery(arena);
            ValueLINQStruct<int> b = DatosConcatB.ToValueQuery(arena);

            List<int> resultado = [];
            foreach (int x in a.Concat(b))
                resultado.Add(x);

            _ = resultado.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void ConcatEagerEntreArenasDistintasCopiaYSobreviveAlCierreDelOrigen()
        {
            // Preparar: el eager no restringe la mezcla de arenas porque copia los operandos a una sesión destino
            // antes de retornar. Esta prueba verifica ese motivo, no solo que la llamada no lance.
            using ValueLINQArena arenaDestino = ValueLINQArena.Crear();
            ValueLINQArena arenaOrigen = ValueLINQArena.Crear();

            using ValueLINQStruct<int> enDestino = DatosConcatA.ToValueQuery(arenaDestino);
            using ValueLINQStruct<int> enOrigen = DatosConcatB.ToValueQuery(arenaOrigen);

            // Actuar
            using ValueLINQStruct<int> concatenado = enDestino.Concat(enOrigen);
            arenaOrigen.Dispose();

            List<int> resultado = [];
            foreach (int x in concatenado)
                resultado.Add(x);

            // Aserción: el resultado vive en la arena del receptor y ya no depende de la del otro operando.
            _ = TokenHelper.ObtenerArenaId(concatenado.Token).Should().Be(arenaDestino.Id);
            _ = concatenado.IsValido.Should().BeTrue("el destino es una copia y no depende de la arena de origen");
            _ = resultado.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void UnaConsultaEagerCreadaEnUnaArenaConservaEsaArenaHastaElFinalDeLaCadena()
        {
            // Preparar: misma propiedad que en el motor perezoso, comprobada sobre el identificador que el token
            // de sesión lleva dentro.
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using ValueLINQStruct<int> inicial = DatosConcatA.ToValueQuery(arena);
            int idInicial = TokenHelper.ObtenerArenaId(inicial.Token);

            // Actuar: cadena completa, con un operando ambiente por medio.
            using ValueLINQStruct<int> filtrado = inicial.Where(0, new MayorQue());
            using ValueLINQStruct<int> proyectado = filtrado.Select<int, Doble, int>(new Doble());
            using ValueLINQStruct<int> ambiente = DatosConcatB.ToValueQuery();
            using ValueLINQStruct<int> concatenado = proyectado.Concat(ambiente);

            // Aserción
            _ = idInicial.Should().Be(arena.Id);
            _ = TokenHelper.ObtenerArenaId(filtrado.Token).Should().Be(idInicial);
            _ = TokenHelper.ObtenerArenaId(proyectado.Token).Should().Be(idInicial);
            _ = TokenHelper.ObtenerArenaId(concatenado.Token).Should().Be(idInicial, "ningún operador puede cambiar dónde almacena la consulta");
        }

        [Fact]
        public void ConcatEagerAdmiteLaArenaAmbienteJuntoAUnaExplicita()
        {
            // Preparar
            using ValueLINQArena arena = ValueLINQArena.Crear();

            // Actuar
            using ValueLINQStruct<int> enArena = DatosConcatA.ToValueQuery(arena);
            using ValueLINQStruct<int> ambiente = DatosConcatB.ToValueQuery();
            using ValueLINQStruct<int> desdeArena = enArena.Concat(ambiente);

            using ValueLINQStruct<int> ambiente2 = DatosConcatA.ToValueQuery();
            using ValueLINQStruct<int> enArena2 = DatosConcatB.ToValueQuery(arena);
            using ValueLINQStruct<int> desdeAmbiente = ambiente2.Concat(enArena2);

            List<int> elementosDesdeArena = [];
            foreach (int x in desdeArena)
                elementosDesdeArena.Add(x);

            List<int> elementosDesdeAmbiente = [];
            foreach (int x in desdeAmbiente)
                elementosDesdeAmbiente.Add(x);

            // Aserción: misma regla que el motor perezoso. La arena ambiente no impone ámbito porque no se puede
            // liberar, así que solo se limita la cantidad de arenas explícitas.
            _ = elementosDesdeArena.Should().Equal(1, 2, 3, 4);
            _ = elementosDesdeAmbiente.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void ConcatEagerVariadicoAdmiteVariasArenasDistintas()
        {
            // Preparar: con cuatro argumentos la resolución de sobrecarga entra por la variante params, que recorre
            // los operandos en bucle. Antes rechazaba la mezcla; ahora tampoco ahí hay nada que restringir.
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            using ValueLINQStruct<int> ambiente = DatosConcatA.ToValueQuery();
            using ValueLINQStruct<int> otroAmbiente = DatosConcatA.ToValueQuery();
            using ValueLINQStruct<int> masAmbiente = DatosConcatA.ToValueQuery();
            using ValueLINQStruct<int> enA = DatosConcatB.ToValueQuery(arenaA);
            using ValueLINQStruct<int> enB = DatosConcatB.ToValueQuery(arenaB);

            // Actuar
            using ValueLINQStruct<int> resultado = ambiente.Concat(otroAmbiente, masAmbiente, enA, enB);

            int elementos = 0;
            foreach (int x in resultado)
                elementos++;

            // Aserción: el destino sigue el receptor, que aquí es ambiente.
            _ = TokenHelper.ObtenerArenaId(resultado.Token).Should().Be(0);
            _ = elementos.Should().Be((DatosConcatA.Length * 3) + (DatosConcatB.Length * 2));
        }

        [Fact]
        public void ArenasDistintasAislanSusSesiones()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            ValueLINQArena arenaB = ValueLINQArena.Crear();

            ValueLINQStruct<int> enA = datos.ToValueQuery(arenaA);
            ValueLINQStruct<int> enB = datos.ToValueQuery(arenaB);

            arenaA.Dispose();

            _ = enA.IsValido.Should().BeFalse("disponer A libera solo lo de A");
            _ = enB.IsValido.Should().BeTrue("lo de B sobrevive");

            arenaB.Dispose();
            _ = enB.IsValido.Should().BeFalse();
        }

#if NET9_0_OR_GREATER
        #region Reaper de arenas

        private static readonly TimeSpan InactividadInmediata = TimeSpan.Zero;
        private static readonly TimeSpan InactividadLarga = TimeSpan.FromHours(1);
        private static readonly int[] DatosReaper = [1, 2, 3];

        [Fact]
        public void RecolectarLiberaUnaArenaVaciaQueSuperoSuUmbral()
        {
            // Preparar: umbral inmediato para no depender del tick de fondo de 10 s.
            ValueLINQArena arena = ValueLINQArena.Crear(inactividad: InactividadInmediata);
            using (ValueLINQStruct<int> consulta = DatosReaper.ToValueQuery(arena))
                _ = consulta.IsValido.Should().BeTrue();

            // Actuar
            int recolectadas = ValueLINQArenaManager.RecolectarArenas();

            // Aserción
            _ = recolectadas.Should().BeGreaterThan(0);
            _ = arena.IsViva.Should().BeFalse("la arena quedó vacía y superó su umbral de inactividad");
        }

        [Fact]
        public void RecolectarNoTocaUnaArenaConSesionesVivas()
        {
            // Preparar: la consulta no se dispone, así que la tabla conserva una sesión.
            ValueLINQArena arena = ValueLINQArena.Crear(inactividad: InactividadInmediata);
            ValueLINQStruct<int> consulta = DatosReaper.ToValueQuery(arena);

            try
            {
                // Actuar
                _ = ValueLINQArenaManager.RecolectarArenas();

                // Aserción
                _ = arena.IsViva.Should().BeTrue("una arena con sesiones vivas no es recolectable aunque supere el umbral");
                _ = consulta.IsValido.Should().BeTrue();
            }
            finally
            {
                arena.Dispose();
            }
        }

        [Fact]
        public void RecolectarNoTocaUnaArenaPersistente()
        {
            // Preparar
            ValueLINQArena arena = ValueLINQArena.Crear(persistente: true, inactividad: InactividadInmediata);

            try
            {
                // Actuar
                _ = ValueLINQArenaManager.RecolectarArenas();

                // Aserción
                _ = arena.IsViva.Should().BeTrue("el indicador persistente excluye la arena del barrido");
            }
            finally
            {
                arena.Dispose();
            }
        }

        [Fact]
        public void RecolectarLiberaUnaArenaCreadaYJamasUsada()
        {
            // Preparar: sin ninguna tabla materializada no hay marca de vaciado, así que la referencia
            // temporal solo puede salir del UltimoUso que sella el alquiler.
            ValueLINQArena arena = ValueLINQArena.Crear(inactividad: InactividadInmediata);

            // Actuar
            _ = ValueLINQArenaManager.RecolectarArenas();

            // Aserción
            _ = arena.IsViva.Should().BeFalse();
        }

        [Fact]
        public void RecolectarEsperaAQueSeVacieLaUltimaTablaDeLaArena()
        {
            // Preparar: dos tipos en la misma arena. La de int se vacía ya; la de string sigue ocupada.
            ValueLINQArena arena = ValueLINQArena.Crear(inactividad: TimeSpan.FromMilliseconds(150));

            ValueLINQStruct<string> enString = DatosReaper.ToValueQuery(arena).Select<int, ATexto, string>(new ATexto());
            using (ValueLINQStruct<int> enInt = DatosReaper.ToValueQuery(arena))
                _ = enInt.IsValido.Should().BeTrue();

            try
            {
                Thread.Sleep(250);

                // Actuar: la tabla de int lleva rato vacía, pero la arena no lo está.
                _ = ValueLINQArenaManager.RecolectarArenas();
                _ = arena.IsViva.Should().BeTrue("todavía queda una tabla con sesiones");

                // Actuar: al vaciar la última tabla, la arena aún no ha cumplido su umbral desde ese instante.
                enString.Dispose();
                _ = ValueLINQArenaManager.RecolectarArenas();
                _ = arena.IsViva.Should().BeTrue("la referencia es el instante en que se vació la ÚLTIMA tabla, no la primera");

                // Actuar: ya con el umbral cumplido desde el vaciado completo.
                Thread.Sleep(250);
                _ = ValueLINQArenaManager.RecolectarArenas();

                // Aserción
                _ = arena.IsViva.Should().BeFalse();
            }
            finally
            {
                if (arena.IsViva)
                    arena.Dispose();
            }
        }

        [Fact]
        public void RecolectarRespetaElUmbralPropioDeCadaArena()
        {
            // Preparar
            ValueLINQArena corta = ValueLINQArena.Crear(inactividad: InactividadInmediata);
            ValueLINQArena larga = ValueLINQArena.Crear(inactividad: InactividadLarga);

            try
            {
                // Actuar
                _ = ValueLINQArenaManager.RecolectarArenas();

                // Aserción: el umbral por arena es lo que aísla unas de otras en un mismo barrido.
                _ = corta.IsViva.Should().BeFalse();
                _ = larga.IsViva.Should().BeTrue();
            }
            finally
            {
                if (larga.IsViva)
                    larga.Dispose();
            }
        }

        #endregion

        #region Motor Delay

        private static readonly int[] DatosChunkDelay = [1, 2, 3, 4, 5, 6];

        [Fact]
        public void DisponerLaArenaLiberaElBufferDeChunkDelay()
        {
            // Preparar: el origen es un array, inmune a la arena, para que lo único que dependa de ella sea el buffer
            // del Chunk. Con un origen de sesión el aserto no probaría nada, porque ValueLINQSessionEnumerator valida
            // su propia sesión en MoveNext y el flujo se secaría por el origen.
            ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQDelayStruct<ReadOnlySpan<int>, ValueLINQChunkDelay<int, ValueLINQSourceEnumerator<int>>> tuberia =
                DatosChunkDelay.ToValueDelayQuery(arena).Chunk(2);

            // Actuar: disponer la arena sin disponer la canalización, que es el caso de la enumeración abandonada.
            arena.Dispose();

            bool lanzo = false;
            int fragmentos = 0;
            ValueLINQChunkDelay<int, ValueLINQSourceEnumerator<int>> enumerador = tuberia.GetEnumerator();

            try
            {
                while (enumerador.MoveNext())
                    fragmentos++;
            }
            catch (ValueLinqArenaInactivaException)
            {
                lanzo = true;
            }

            // Aserción: si el buffer no viviera en la arena, la enumeración seguiría entregando fragmentos.
            _ = lanzo.Should().BeTrue("el buffer del Chunk debe vivir en la arena y liberarse en bloque con ella");
            _ = fragmentos.Should().Be(0);
        }

        [Fact]
        public void LaTuberiaDelayHeredaLaArenaDeLaConsultaEager()
        {
            // Preparar
            using ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQStruct<int> consulta = DatosChunkDelay.ToValueQuery(arena);

            // Actuar
            ValueLINQDelayStruct<int, ValueLINQSessionEnumerator<int>> tuberia = consulta.Delay();

            // Aserción: se comprueba el estado propagado y no la enumeración, porque disponer la arena secaría también
            // el enumerador de sesión de origen y el aserto no distinguiría una causa de la otra.
            _ = tuberia._opciones.HasArena.Should().BeTrue("la canalización debe heredar la arena de la sesión de origen");
            _ = tuberia._opciones.IdArena.Should().Be(arena.Id);
        }

        [Fact]
        public void LaTuberiaDelaySinArenaUsaLaArenaAmbiente()
        {
            // Preparar y actuar
            ValueLINQDelayStruct<ReadOnlySpan<int>, ValueLINQChunkDelay<int, ValueLINQSourceEnumerator<int>>> tuberia =
                DatosChunkDelay.ToValueDelayQuery().Chunk(2);

            int fragmentos = 0;
            ValueLINQChunkDelay<int, ValueLINQSourceEnumerator<int>> enumerador = tuberia.GetEnumerator();
            while (enumerador.MoveNext())
                fragmentos++;

            // Aserción
            _ = tuberia._opciones.HasArena.Should().BeFalse();
            _ = fragmentos.Should().Be(3, "seis elementos en fragmentos de dos");
        }

        [Fact]
        public void ChunkSobreUnaArenaYaDispuestaLanzaArenaInactiva()
        {
            // Preparar
            ValueLINQArena arena = ValueLINQArena.Crear();
            arena.Dispose();

            // Actuar: la canalización es un ref struct y no se puede capturar, así que se construye dentro del lambda.
            Action actChunk = () => _ = DatosChunkDelay.ToValueDelayQuery(arena).Chunk(2);

            // Aserción
            _ = actChunk.Should().Throw<ValueLinqArenaInactivaException>();
        }

        [Fact]
        public void ConcatDelayEntreArenasDistintasLanzaArenaCruzada()
        {
            // Preparar
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            // Actuar
            Action actConcat = () => _ = DatosConcatA.ToValueDelayQuery(arenaA).Concat(DatosConcatB.ToValueDelayQuery(arenaB));

            // Aserción
            _ = actConcat.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        [Fact]
        public void ConcatDelayConDosArenasDistintasEntreLosArgumentosLanzaArenaCruzada()
        {
            // Preparar
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            // Actuar: el receptor es ambiente, así que la discrepancia solo está entre los argumentos.
            Action actConcat = () => _ = DatosChunkDelay.ToValueDelayQuery()
                .Concat(DatosConcatA.ToValueDelayQuery(arenaA), DatosConcatB.ToValueDelayQuery(arenaB));

            // Aserción
            _ = actConcat.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        [Fact]
        public void ConcatDelayConservaLaArenaDelReceptorAunqueLosArgumentosTraiganOtra()
        {
            // Preparar
            using ValueLINQArena arena = ValueLINQArena.Crear();

            // Actuar: el receptor define dónde almacena el flujo; los argumentos solo aportan datos.
            ValueLINQDelayStruct<int, ValueLINQConcatDelay<int, ValueLINQConcatDelay<int, ValueLINQSourceEnumerator<int>, ValueLINQSourceEnumerator<int>>, ValueLINQSourceEnumerator<int>>> tuberia =
                DatosChunkDelay.ToValueDelayQuery()
                    .Concat(DatosConcatA.ToValueDelayQuery(arena), DatosConcatB.ToValueDelayQuery(arena));

            // Aserción
            _ = tuberia._opciones.HasArena.Should().BeFalse("el receptor era ambiente y los argumentos no cambian el almacenamiento del flujo");
        }

        [Fact]
        public void ConcatDelayAdmiteLaArenaAmbienteJuntoAUnaExplicita()
        {
            // Preparar: la arena ambiente vive hasta el fin del proceso, así que mezclarla con una explícita no
            // introduce una segunda vida útil que pueda expirar. Solo se restringe una segunda arena explícita.
            using ValueLINQArena arena = ValueLINQArena.Crear();

            // Actuar
            ValueLINQDelayStruct<int, ValueLINQConcatDelay<int, ValueLINQSourceEnumerator<int>, ValueLINQSourceEnumerator<int>>> tuberia =
                DatosConcatA.ToValueDelayQuery(arena).Concat(DatosConcatB.ToValueDelayQuery());

            List<int> resultado = [];
            foreach (ref readonly int item in tuberia)
                resultado.Add(item);

            // Aserción: el receptor sigue mandando, y su arena explícita se conserva.
            _ = resultado.Should().Equal(1, 2, 3, 4);
            _ = tuberia._opciones.HasArena.Should().BeTrue();
            _ = tuberia._opciones.IdArena.Should().Be(arena.Id);
        }

        [Fact]
        public void UnaTuberiaCreadaEnUnaArenaConservaEsaArenaHastaElFinalDeLaCadena()
        {
            // Preparar: la restricción de arena única se sostiene sobre que el almacenamiento siga al receptor, que
            // es una decisión tomada en otro sitio. Esta prueba fija la propiedad final en vez del rechazo del Concat,
            // para que romper la propagación no pase inadvertido.
            using ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQDelayStruct<int, ValueLINQSourceEnumerator<int>> inicial = DatosChunkDelay.ToValueDelayQuery(arena);
            int idInicial = inicial._opciones.IdArena;

            // Actuar: cadena completa, incluidos operandos ambientes y un operador que reserva.
            var final = inicial
                .Where<MayorQue, int>(0)
                .Select<Doble, int>()
                .Concat(DatosConcatA.ToValueDelayQuery(), DatosConcatB.ToValueDelayQuery())
                .Chunk(2);

            // Aserción
            _ = idInicial.Should().Be(arena.Id);
            _ = final._opciones.IdArena.Should().Be(idInicial, "ningún operador puede cambiar dónde almacena la consulta");
        }

        [Fact]
        public void ConcatDelayEncadenadoDesdeRaizAmbienteAbarcaVariasArenas()
        {
            // Preparar: la comprobación es por operación contra el receptor, no un invariante global de la consulta.
            // Con la raíz ambiente ninguna comparación llega a ver dos arenas explícitas a la vez.
            using ValueLINQArena arena1 = ValueLINQArena.Crear();
            using ValueLINQArena arena2 = ValueLINQArena.Crear();

            // Actuar
            var tuberia = DatosConcatA.ToValueDelayQuery()
                .Concat(DatosConcatB.ToValueDelayQuery(arena1))
                .Concat(DatosChunkDelay.ToValueDelayQuery(arena2));

            List<int> resultado = [];
            foreach (ref readonly int item in tuberia)
                resultado.Add(item);

            // Aserción: se tolera porque la canalización sigue siendo ambiente y nunca reserva en las arenas
            // mezcladas, solo lee de ellas.
            _ = resultado.Should().Equal(1, 2, 3, 4, 1, 2, 3, 4, 5, 6);
            _ = tuberia._opciones.HasArena.Should().BeFalse();
        }

        [Fact]
        public void ConcatDelayEncadenadoDesdeRaizExplicitaRechazaUnaSegundaArena()
        {
            // Preparar: con la raíz en una arena, el receptor la conserva y la segunda explícita sí choca.
            using ValueLINQArena arena1 = ValueLINQArena.Crear();
            using ValueLINQArena arena2 = ValueLINQArena.Crear();

            // Actuar
            Action actConcat = () => _ = DatosConcatA.ToValueDelayQuery(arena1)
                .Concat(DatosConcatB.ToValueDelayQuery())
                .Concat(DatosChunkDelay.ToValueDelayQuery(arena2));

            // Aserción
            _ = actConcat.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        [Fact]
        public void ReasignarLaArenaDeUnaTuberiaQueYaTieneUnaLanzaArenaCruzada()
        {
            // Preparar
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            ValueLINQDelayOptions opciones = ValueLINQDelayOptions.Ambiente.DesdeArena(arenaA);

            // Actuar
            Action actReasignar = () => opciones.DesdeArena(arenaB);

            // Aserción: una canalización solo puede adquirir arena desde el estado ambiente.
            _ = actReasignar.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        #endregion

        #region Escotilla de Marshalling

        [Fact]
        public void SinComprobarLimitesDeArenaPermiteMezclarDosArenasExplicitas()
        {
            // Preparar
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            // Actuar
            var tuberia = DatosConcatA.ToValueDelayQuery(arenaA)
                .SinComprobarLimitesDeArena()
                .Concat(DatosConcatB.ToValueDelayQuery(arenaB));

            List<int> resultado = [];
            foreach (ref readonly int item in tuberia)
                resultado.Add(item);

            // Aserción
            _ = resultado.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void SinComprobarLimitesDeArenaSePropagaALosOperadoresPosteriores()
        {
            // Preparar: la renuncia es propiedad de la canalización, no de la llamada siguiente. Aquí se aplica
            // antes de Where y Select, y el Concat con otra arena llega varios operadores después.
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            // Actuar
            var tuberia = DatosChunkDelay.ToValueDelayQuery(arenaA)
                .SinComprobarLimitesDeArena()
                .Where<MayorQuePredicadoArena, int>(0)
                .Select<DobleArena, int>()
                .Concat(DatosConcatB.ToValueDelayQuery(arenaB).Select<DobleArena, int>());

            List<int> resultado = [];
            foreach (ref readonly int item in tuberia)
                resultado.Add(item);

            // Aserción
            _ = resultado.Should().Equal(2, 4, 6, 8, 10, 12, 6, 8);
        }

        [Fact]
        public void SinComprobarLimitesDeArenaNoCambiaLaArenaDeAlmacenamiento()
        {
            // Preparar
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            // Actuar
            var tuberia = DatosConcatA.ToValueDelayQuery(arenaA)
                .SinComprobarLimitesDeArena()
                .Concat(DatosConcatB.ToValueDelayQuery(arenaB));

            // Aserción: renunciar al recuento no mueve dónde almacena la consulta.
            _ = tuberia._opciones.IdArena.Should().Be(arenaA.Id);
        }

        [Fact]
        public void SinComprobarLimitesDeArenaNoDesactivaLaValidacionDeSesion()
        {
            // Preparar: con la renuncia puesta, el Chunk sigue reservando en la arena del receptor y sigue
            // comprobando que esa arena vive. La red de seguridad no se apaga.
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            var tuberia = DatosChunkDelay.ToValueDelayQuery(arenaA)
                .SinComprobarLimitesDeArena()
                .Concat(DatosConcatB.ToValueDelayQuery(arenaB))
                .Chunk(2);

            // Actuar
            arenaA.Dispose();

            bool lanzo = false;
            var enumerador = tuberia.GetEnumerator();

            try
            {
                while (enumerador.MoveNext()) { }
            }
            catch (ValueLinqArenaInactivaException)
            {
                lanzo = true;
            }

            // Aserción
            _ = lanzo.Should().BeTrue("la renuncia solo apaga el recuento de arenas, no la validación de la sesión");
        }

        [Fact]
        public void SinComprobarLimitesDeArenaNoDesactivaLaComprobacionDeArenaVivaAlReservar()
        {
            // Preparar: la red anterior salta durante la enumeración. Esta es la otra, la que Chunk aplica al
            // reservar su buffer, y por eso la arena se dispone ANTES de construir el operador.
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            arenaA.Dispose();

            // Actuar: la canalización es un ref struct y no se puede capturar, así que se construye dentro del lambda.
            Action actChunk = () => _ = DatosChunkDelay.ToValueDelayQuery(arenaA)
                .SinComprobarLimitesDeArena()
                .Concat(DatosConcatB.ToValueDelayQuery(arenaB))
                .Chunk(2);

            // Aserción
            _ = actChunk.Should().Throw<ValueLinqArenaInactivaException>("reservar en una arena liberada sigue prohibido con la renuncia puesta");
        }

        [Fact]
        public void SinComprobarLimitesDeArenaNoPermiteReasignarLaArena()
        {
            // Preparar: el estado de la consulta nunca debe fragmentarse entre arenas, así que reasignar la arena
            // de almacenamiento sigue prohibido aunque se haya renunciado al recuento.
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            ValueLINQDelayOptions opciones = ValueLINQDelayOptions.Ambiente
                .DesdeArena(arenaA)
                .SinComprobarLimitesDeArena();

            // Actuar
            Action actReasignar = () => opciones.DesdeArena(arenaB);

            // Aserción
            _ = actReasignar.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        [Fact]
        public void ChunkRechazaUnaEncarnacionRecicladaAunqueLaTablaSigaMaterializada()
        {
            // Preparar: el caso exclusivo del guardián de ValueLINQChunkDelay. La arena sigue viva y su tabla
            // materializada, así que ObtenerOCrearTabla devolvería la tabla sin quejarse; solo comparar el token
            // completo detecta que estas opciones apuntan a una encarnación anterior del mismo identificador.
            using ValueLINQArena arena = ValueLINQArena.Crear();
            using ValueLINQStruct<int> materializaLaTabla = DatosChunkDelay.ToValueQuery(arena);

            long tokenVivo = TokenHelper.CrearTokenArena(arena.Id, ValueLINQArenaManager.ObtenerGeneracion(arena.Id));
            long tokenEncarnacionAnterior = TokenHelper.CrearTokenArena(arena.Id, ValueLINQArenaManager.ObtenerGeneracion(arena.Id) - 1);

            _ = ValueLINQArenaManager.IsArenaViva(tokenVivo).Should().BeTrue("la arena sigue viva y su tabla existe");

            // Actuar: las opciones se forjan escribiendo sus campos, que ya son internos, en lugar de pedirle a la
            // librería un constructor en crudo. La maniobra invasiva vive en la prueba y el tipo no expone nada nuevo.
            Action actChunk = () =>
            {
                ValueLINQSourceEnumerator<int> origen = new(DatosChunkDelay);

                ValueLINQDelayOptions opcionesForjadas = default;
                Unsafe.AsRef(in opcionesForjadas.TokenArena) = tokenEncarnacionAnterior;

                _ = new ValueLINQChunkDelay<int, ValueLINQSourceEnumerator<int>>(ref origen, 2, opcionesForjadas);
            };

            // Aserción
            _ = actChunk.Should().Throw<ValueLinqArenaInactivaException>(
                "reservar sobre una encarnación anterior debe rechazarse aunque la tabla de ese identificador exista");
        }

        private struct MayorQuePredicadoArena : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int umbral) => item > umbral;
        }

        private struct DobleArena : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        #endregion
#endif
    }
}
