using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class TablaSesionesTests
    {
        [Fact]
        public void PrimeraSesionCodificaElIndiceGlobalEnElToken()
        {
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
            long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
            try
            {
                TablaSesiones<int> tabla = new(idArena, genArena);

                long token = tabla.ObtenerMetadatos(4).Token;

                _ = TokenHelper.ObtenerSlotIndex(token).Should().Be(ValueLINQConfig.Slots - 1);
                _ = TokenHelper.ObtenerArenaId(token).Should().Be(idArena);
                _ = TokenHelper.ObtenerVersion(token).Should().Be(1);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArena);
            }
        }

        [Fact]
        public void SesionesConsecutivasDevuelvenSlotsGlobalesUnicosDescendentes()
        {
            TablaSesiones<int> tabla = new(0);

            long token1 = tabla.ObtenerMetadatos(4).Token;
            long token2 = tabla.ObtenerMetadatos(4).Token;
            long token3 = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(token1).Should().Be(4095);
            _ = TokenHelper.ObtenerSlotIndex(token2).Should().Be(4094);
            _ = TokenHelper.ObtenerSlotIndex(token3).Should().Be(4093);
        }

        [Fact]
        public void SoloSeMaterializaLaParticionTocada()
        {
            TablaSesiones<int> tabla = new(0);
            _ = tabla.ObtenerMetadatos(1);

            int particionViva = (ValueLINQConfig.Slots - 1) >> ValueLINQConfig.SlotsParticionBits;

            _ = tabla.IsParticionMaterializada(particionViva).Should().BeTrue();
            _ = tabla.IsParticionMaterializada(0).Should().BeFalse();

            Action accederParticionInexistente = () => _ = tabla.ObtenerMetadatos(TokenHelper.CrearToken(0, 0, 1L)).IsDisposed;
            _ = accederParticionInexistente.Should().Throw<ValueLinqSesionExpiradaException>();
        }

        [Fact]
        public async Task ContencionCruzandoLaFronteraDeParticionProduceSesionesUnicas()
        {
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
            long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
            try
            {
                TablaSesiones<int> tabla = new(idArena, genArena);
                const int hilos = 16;
                const int sesionesPorHilo = 40;
                ConcurrentBag<long> tokens = [];
                using Barrier barrera = new(hilos);

                Task[] tareas = new Task[hilos];
                for (int t = 0; t < hilos; t++)
                {
                    tareas[t] = Task.Run(() =>
                    {
                        barrera.SignalAndWait();
                        for (int i = 0; i < sesionesPorHilo; i++)
                            tokens.Add(tabla.ObtenerMetadatos(4).Token);
                    });
                }

                await Task.WhenAll(tareas);

                int totalEsperado = hilos * sesionesPorHilo;
                _ = tokens.Should().HaveCount(totalEsperado);
                _ = tokens.Distinct().Should().HaveCount(totalEsperado);
                _ = tokens.Select(TokenHelper.ObtenerSlotIndex).Distinct().Should().HaveCount(totalEsperado);
                _ = tokens.Should().OnlyContain(token => TokenHelper.ObtenerArenaId(token) == idArena);
                _ = tokens.Should().NotContain(0L);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArena);
            }
        }

        [Fact]
        public async Task ContencionSoloEnLaFronteraExactaDeParticion()
        {
            TablaSesiones<int> tabla = new(0);
            for (int i = 0; i < ValueLINQConfig.SlotsEnParticion - 8; i++)
                _ = tabla.ObtenerMetadatos(1);

            const int hilos = 16;
            ConcurrentBag<long> tokens = [];
            using Barrier barrera = new(hilos);

            Task[] tareas = new Task[hilos];
            for (int t = 0; t < hilos; t++)
            {
                tareas[t] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    tokens.Add(tabla.ObtenerMetadatos(4).Token);
                });
            }

            await Task.WhenAll(tareas);

            _ = tokens.Should().HaveCount(hilos);
            _ = tokens.Select(TokenHelper.ObtenerSlotIndex).Distinct().Should().HaveCount(hilos);
        }

        [Fact]
        public void AgotarLaCapacidadLanzaInvalidOperationConMensajeClaro()
        {
            TablaSesiones<int> tabla = new(0);
            for (int i = 0; i < ValueLINQConfig.Slots; i++)
                _ = tabla.ObtenerMetadatos(1);

            Action alquilerExtra = () => _ = tabla.ObtenerMetadatos(1);

            _ = alquilerExtra.Should().Throw<InvalidOperationException>().WithMessage("*Capacidad máxima*");
        }

        [Fact]
        public void TablasDistintasSonIndependientes()
        {
            long tokenArena1 = ValueLINQArenaManager.Alquilar();
            long tokenArena2 = ValueLINQArenaManager.Alquilar();
            int idArena1 = TokenHelper.ObtenerIdTokenArena(tokenArena1);
            int idArena2 = TokenHelper.ObtenerIdTokenArena(tokenArena2);
            long genArena1 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena1);
            long genArena2 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena2);
            try
            {
                TablaSesiones<int> tabla1 = new(idArena1, genArena1);
                TablaSesiones<int> tabla2 = new(idArena2, genArena2);

                long token1 = tabla1.ObtenerMetadatos(4).Token;
                long token2 = tabla2.ObtenerMetadatos(4).Token;

                _ = TokenHelper.ObtenerSlotIndex(token1).Should().Be(TokenHelper.ObtenerSlotIndex(token2));
                _ = TokenHelper.ObtenerArenaId(token1).Should().NotBe(TokenHelper.ObtenerArenaId(token2));
                _ = token1.Should().NotBe(token2);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArena1);
                ValueLINQArenaManager.Liberar(tokenArena2);
            }
        }

        [Fact]
        public void DobleLiberacionNoEmpujaElSlotDosVeces()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;

            tabla.LiberarMetadatos(token);
            tabla.LiberarMetadatos(token);

            long tokenA = tabla.ObtenerMetadatos(4).Token;
            long tokenB = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(tokenA).Should().NotBe(TokenHelper.ObtenerSlotIndex(tokenB));
        }

        [Fact]
        public void TokenRancioNoLiberaLaSesionVivaDelSlotReciclado()
        {
            TablaSesiones<int> tabla = new(0);
            long tokenViejo = tabla.ObtenerMetadatos(4).Token;
            tabla.LiberarMetadatos(tokenViejo);

            long tokenVivo = tabla.ObtenerMetadatos(4).Token;

            tabla.LiberarMetadatos(tokenViejo);

            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(tokenVivo);
            _ = metadatos.Token.Should().Be(tokenVivo);
            _ = metadatos.IsDisposed.Should().BeFalse();
            _ = metadatos.Array.Should().NotBeNull();

            long tokenSiguiente = tabla.ObtenerMetadatos(4).Token;
            _ = TokenHelper.ObtenerSlotIndex(tokenSiguiente).Should().NotBe(TokenHelper.ObtenerSlotIndex(tokenVivo));
        }

        [Fact]
        public void ReciclarUnSlotIncrementaLaVersionYMataAlTokenViejo()
        {
            TablaSesiones<int> tabla = new(0);
            long token1 = tabla.ObtenerMetadatos(4).Token;
            tabla.LiberarMetadatos(token1);

            long token2 = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(token2).Should().Be(TokenHelper.ObtenerSlotIndex(token1));
            _ = TokenHelper.ObtenerVersion(token2).Should().Be(TokenHelper.ObtenerVersion(token1) + 1);
            _ = token2.Should().NotBe(token1);
        }

        [Fact]
        public async Task LiberacionConcurrenteDelMismoTokenSoloGanaUnHilo()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;

            const int hilos = 16;
            using Barrier barrera = new(hilos);
            Task[] tareas = new Task[hilos];
            for (int t = 0; t < hilos; t++)
            {
                tareas[t] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    tabla.LiberarMetadatos(token);
                });
            }

            await Task.WhenAll(tareas);

            long tokenA = tabla.ObtenerMetadatos(4).Token;
            long tokenB = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(tokenA).Should().NotBe(TokenHelper.ObtenerSlotIndex(tokenB));
        }

        [Fact]
        public void CicloMasivoDeAdquisicionYLiberacionNoFugaSlotsNiVersiones()
        {
            TablaSesiones<int> tabla = new(0);
            const int ciclos = 10_000;

            for (int i = 0; i < ciclos; i++)
            {
                long token = tabla.ObtenerMetadatos(4).Token;
                tabla.LiberarMetadatos(token);
            }

            long tokenFinal = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(tokenFinal).Should().Be(ValueLINQConfig.Slots - 1);
            _ = TokenHelper.ObtenerVersion(tokenFinal).Should().Be(ciclos + 1);
        }

        [Fact]
        public void AsegurarEspacioCreceElArrayPreservandoLosDatos()
        {
            TablaSesiones<int> tabla = new(0);
            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(4);
            long token = metadatos.Token;

            metadatos.Array![0] = 11;
            metadatos.Array[1] = 22;
            metadatos.Array[2] = 33;
            metadatos.TamañoActual = 3;
            int[] arrayOriginal = metadatos.Array;

            tabla.AsegurarEspacio(token, 10_000);

            ref MetadatosSesion<int> despues = ref tabla.ObtenerMetadatos(token);
            _ = despues.Array.Should().NotBeSameAs(arrayOriginal);
            _ = despues.Array!.Length.Should().BeGreaterThanOrEqualTo(10_000);
            _ = despues.Array[0].Should().Be(11);
            _ = despues.Array[1].Should().Be(22);
            _ = despues.Array[2].Should().Be(33);
            _ = despues.TamañoActual.Should().Be(3);
        }

        [Fact]
        public void AsegurarEspacioConCapacidadSuficienteNoCambiaElArray()
        {
            TablaSesiones<int> tabla = new(0);
            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(64);
            long token = metadatos.Token;
            int[] arrayOriginal = metadatos.Array!;

            tabla.AsegurarEspacio(token, 8);

            _ = tabla.ObtenerMetadatos(token).Array.Should().BeSameAs(arrayOriginal);
        }

        [Fact]
        public void AsegurarEspacioConTokensMuertosLanzaLaExcepcionCorrecta()
        {
            TablaSesiones<int> tabla = new(0);
            long tokenLiberado = tabla.ObtenerMetadatos(4).Token;
            tabla.LiberarMetadatos(tokenLiberado);

            Action conTokenCero = () => tabla.AsegurarEspacio(0L, 16);
            Action conTokenLiberado = () => tabla.AsegurarEspacio(tokenLiberado, 16);
            Action conParticionInexistente = () => tabla.AsegurarEspacio(TokenHelper.CrearToken(0, 0, 1L), 16);

            _ = conTokenCero.Should().Throw<ValueLinqTokenInvalidoException>();
            _ = conTokenLiberado.Should().Throw<ValueLinqSesionExpiradaException>();
            _ = conParticionInexistente.Should().Throw<ValueLinqSesionExpiradaException>();
        }

        [Fact]
        public async Task AsegurarEspacioConcurrenteSobreElMismoTokenPreservaLosDatos()
        {
            TablaSesiones<int> tabla = new(0);
            // El acceso por ref se aísla en funciones locales síncronas: C# 12 (net8.0) prohíbe
            // ref locals dentro de un método async, y esta prueba multitarget hasta net8.0.
            long token = PrepararSesion(tabla);

            const int hilos = 16;
            using Barrier barrera = new(hilos);
            Task[] tareas = new Task[hilos];
            for (int t = 0; t < hilos; t++)
            {
                int tamaño = (t + 1) * 500;
                tareas[t] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    tabla.AsegurarEspacio(token, tamaño);
                });
            }

            await Task.WhenAll(tareas);

            VerificarSesion(tabla, token, hilos);
            _ = tabla.IsMetadatoValido(token).Should().BeTrue();

            static long PrepararSesion(TablaSesiones<int> tabla)
            {
                ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(4);
                metadatos.Array![0] = 77;
                metadatos.TamañoActual = 1;
                return metadatos.Token;
            }

            static void VerificarSesion(TablaSesiones<int> tabla, long token, int hilos)
            {
                ref MetadatosSesion<int> despues = ref tabla.ObtenerMetadatos(token);
                _ = despues.Array!.Length.Should().BeGreaterThanOrEqualTo(hilos * 500);
                _ = despues.Array[0].Should().Be(77);
                _ = despues.TamañoActual.Should().Be(1);
            }
        }

        [Fact]
        public void LimpiarSesionesExpiradasLiberaSoloLasCaducadas()
        {
            TablaSesiones<int> tabla = new(0);
            long caducada1 = tabla.ObtenerMetadatos(4).Token;
            long caducada2 = tabla.ObtenerMetadatos(4).Token;
            long viva = tabla.ObtenerMetadatos(4).Token;

            long haceUnaHora = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3600;
            tabla.ObtenerMetadatos(caducada1).UltimoAcceso = haceUnaHora;
            tabla.ObtenerMetadatos(caducada2).UltimoAcceso = haceUnaHora;

            tabla.LimpiarSesionesExpiradas(TimeSpan.FromMinutes(5));

            _ = tabla.IsMetadatoValido(caducada1).Should().BeFalse();
            _ = tabla.IsMetadatoValido(caducada2).Should().BeFalse();
            _ = tabla.IsMetadatoValido(viva).Should().BeTrue();
        }

        [Fact]
        public void LosSlotsLimpiadosVuelvenAlStackYReciclanLaVersion()
        {
            TablaSesiones<int> tabla = new(0);
            long caducada = tabla.ObtenerMetadatos(4).Token;
            tabla.ObtenerMetadatos(caducada).UltimoAcceso = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3600;

            tabla.LimpiarSesionesExpiradas(TimeSpan.FromMinutes(5));
            long reciclada = tabla.ObtenerMetadatos(4).Token;

            _ = TokenHelper.ObtenerSlotIndex(reciclada).Should().Be(TokenHelper.ObtenerSlotIndex(caducada));
            _ = TokenHelper.ObtenerVersion(reciclada).Should().Be(TokenHelper.ObtenerVersion(caducada) + 1);
            _ = tabla.IsMetadatoValido(caducada).Should().BeFalse();
            _ = tabla.IsMetadatoValido(reciclada).Should().BeTrue();
        }

        [Fact]
        public void SesionCaducadaPeroRefrescadaSobreviveAlBarrido()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;
            tabla.ObtenerMetadatos(token).UltimoAcceso = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 3600;

            tabla.RefrescarUltimoAcceso(token, TimeSpan.Zero);
            tabla.LimpiarSesionesExpiradas(TimeSpan.FromMinutes(5));

            _ = tabla.IsMetadatoValido(token).Should().BeTrue();
        }

        [Fact]
        public void BarrerSinCandidatosNoTocaNada()
        {
            TablaSesiones<int> tablaVacia = new(0);
            TablaSesiones<int> tablaConVivas = new(0);
            long viva = tablaConVivas.ObtenerMetadatos(4).Token;

            Action barrerVacia = () => tablaVacia.LimpiarSesionesExpiradas(TimeSpan.FromMinutes(5));

            _ = barrerVacia.Should().NotThrow();
            tablaConVivas.LimpiarSesionesExpiradas(TimeSpan.FromMinutes(5));
            _ = tablaConVivas.IsMetadatoValido(viva).Should().BeTrue();
        }

        [Fact]
        public void AñadirElementosYSpanConstruyenElContenidoEnOrden()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;

            tabla.Añadir(token, 1);
            tabla.Añadir(token, 2);
            tabla.Añadir(token, [3, 4, 5]);

            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(token);
            _ = metadatos.TamañoActual.Should().Be(5);
            _ = metadatos.Array!.AsSpan(0, 5).ToArray().Should().Equal(1, 2, 3, 4, 5);
        }

        [Fact]
        public void AñadirMasAllaDeLaCapacidadCrecePreservandoLosDatos()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;
            int[] arrayInicial = tabla.ObtenerMetadatos(token).Array!;

            for (int i = 0; i < 100; i++)
                tabla.Añadir(token, i);

            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(token);
            _ = metadatos.Array.Should().NotBeSameAs(arrayInicial);
            _ = metadatos.TamañoActual.Should().Be(100);
            _ = metadatos.Array!.AsSpan(0, 100).ToArray().Should().Equal(Enumerable.Range(0, 100));
        }

        [Fact]
        public void AñadirConTokensMuertosLanzaLaExcepcionCorrecta()
        {
            TablaSesiones<int> tabla = new(0);
            long liberado = tabla.ObtenerMetadatos(4).Token;
            tabla.LiberarMetadatos(liberado);
            long fuenteViva = tabla.ObtenerMetadatos(4).Token;
            tabla.Añadir(fuenteViva, 9);

            Action elementoTokenCero = () => tabla.Añadir(0L, 1);
            Action elementoLiberado = () => tabla.Añadir(liberado, 1);
            Action spanLiberado = () => tabla.Añadir(liberado, new[] { 1, 2 });
            Action concatDestinoMuerto = () => tabla.Añadir(liberado, fuenteViva);

            _ = elementoTokenCero.Should().Throw<ValueLinqTokenInvalidoException>();
            _ = elementoLiberado.Should().Throw<ValueLinqSesionExpiradaException>();
            _ = spanLiberado.Should().Throw<ValueLinqSesionExpiradaException>();
            _ = concatDestinoMuerto.Should().Throw<ValueLinqSesionExpiradaException>();
        }

        [Fact]
        public void ConcatCopiaElContenidoYNoTocaLaFuente()
        {
            TablaSesiones<int> tabla = new(0);
            long destino = tabla.ObtenerMetadatos(4).Token;
            long fuente = tabla.ObtenerMetadatos(4).Token;
            tabla.Añadir(destino, [1, 2]);
            tabla.Añadir(fuente, [3, 4, 5]);

            tabla.Añadir(destino, fuente);

            ref MetadatosSesion<int> d = ref tabla.ObtenerMetadatos(destino);
            _ = d.TamañoActual.Should().Be(5);
            _ = d.Array!.AsSpan(0, 5).ToArray().Should().Equal(1, 2, 3, 4, 5);

            ref MetadatosSesion<int> f = ref tabla.ObtenerMetadatos(fuente);
            _ = f.TamañoActual.Should().Be(3);
            _ = tabla.IsMetadatoValido(fuente).Should().BeTrue();
        }

        [Fact]
        public void ConcatConsigoMismoDuplicaElContenido()
        {
            TablaSesiones<int> tabla = new(0);
            long token = tabla.ObtenerMetadatos(4).Token;
            tabla.Añadir(token, [1, 2, 3]);

            tabla.Añadir(token, token);

            ref MetadatosSesion<int> metadatos = ref tabla.ObtenerMetadatos(token);
            _ = metadatos.TamañoActual.Should().Be(6);
            _ = metadatos.Array!.AsSpan(0, 6).ToArray().Should().Equal(1, 2, 3, 1, 2, 3);
        }

        [Fact]
        public void ConcatConDestinoTokenCeroLanzaTokenInvalido()
        {
            TablaSesiones<int> tabla = new(0);
            long fuenteViva = tabla.ObtenerMetadatos(4).Token;
            tabla.Añadir(fuenteViva, 9);
            long fuenteVacia = tabla.ObtenerMetadatos(4).Token;

            Action conFuenteViva = () => tabla.Añadir(0L, fuenteViva);
            Action conFuenteVacia = () => tabla.Añadir(0L, fuenteVacia);

            _ = conFuenteViva.Should().Throw<ValueLinqTokenInvalidoException>();
            _ = conFuenteVacia.Should().Throw<ValueLinqTokenInvalidoException>();
        }

        [Fact]
        public void ConcatConFuentesVaciasEsInofensivo()
        {
            TablaSesiones<int> tabla = new(0);
            long destino = tabla.ObtenerMetadatos(4).Token;
            tabla.Añadir(destino, 7);
            long fuenteVacia = tabla.ObtenerMetadatos(4).Token;

            tabla.Añadir(destino, 0L);
            tabla.Añadir(destino, fuenteVacia);

            ref MetadatosSesion<int> d = ref tabla.ObtenerMetadatos(destino);
            _ = d.TamañoActual.Should().Be(1);
            _ = d.Array![0].Should().Be(7);
        }

        [Fact]
        public async Task CruceInversoDeConcatNoInterbloquea()
        {
            TablaSesiones<int> tabla = new(0);
            const int iteraciones = 200;

            (long a, long b)[] pares = new (long, long)[iteraciones];
            for (int i = 0; i < iteraciones; i++)
            {
                long a = tabla.ObtenerMetadatos(4).Token;
                long b = tabla.ObtenerMetadatos(4).Token;
                tabla.Añadir(a, 1);
                tabla.Añadir(b, [2, 3]);
                pares[i] = (a, b);
            }

            using Barrier barrera = new(2);
            Task[] tareas =
            [
                Task.Run(() =>
                {
                    for (int i = 0; i < iteraciones; i++)
                    {
                        barrera.SignalAndWait();
                        tabla.Añadir(pares[i].a, pares[i].b);
                    }
                }),
                Task.Run(() =>
                {
                    for (int i = 0; i < iteraciones; i++)
                    {
                        barrera.SignalAndWait();
                        tabla.Añadir(pares[i].b, pares[i].a);
                    }
                }),
            ];

            Task todas = Task.WhenAll(tareas);
            bool completado = await Task.WhenAny(todas, Task.Delay(TimeSpan.FromSeconds(30))) == todas;

            _ = completado.Should().BeTrue("el orden global de adquisición debe impedir el interbloqueo del cruce inverso");
            await todas; // Propaga cualquier excepción de las tareas, como hacía Task.WaitAll

            foreach ((long a, long b) in pares)
            {
                int suma = tabla.ObtenerMetadatos(a).TamañoActual + tabla.ObtenerMetadatos(b).TamañoActual;
                _ = suma.Should().BeOneOf(7, 8);
            }
        }

        [Fact]
        public void DosEncarnacionesDeUnaArenaNoProducenTokensColisionados()
        {
            long tokenArena1 = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena1);
            long gen1 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena1);

            TablaSesiones<int> encarnacion1 = new(idArena, gen1);
            long t1 = encarnacion1.ObtenerMetadatos(4).Token;

            ValueLINQArenaManager.Liberar(tokenArena1);

            List<long> arenasTemporales = [];
            long tokenArena2 = 0L;
            try
            {
                while (true)
                {
                    long tok = ValueLINQArenaManager.Alquilar();
                    if (TokenHelper.ObtenerIdTokenArena(tok) == idArena)
                    {
                        tokenArena2 = tok;
                        break;
                    }

                    arenasTemporales.Add(tok);
                }

                long gen2 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena2);
                TablaSesiones<int> encarnacion2 = new(idArena, gen2);
                long t2 = encarnacion2.ObtenerMetadatos(4).Token;

                _ = TokenHelper.ObtenerSlotIndex(t1).Should().Be(TokenHelper.ObtenerSlotIndex(t2));
                _ = TokenHelper.ObtenerVersion(t1).Should().Be(TokenHelper.ObtenerVersion(t2));
                _ = TokenHelper.ObtenerArenaId(t1).Should().Be(TokenHelper.ObtenerArenaId(t2));
                _ = t1.Should().NotBe(t2, "la generación de arena debe desambiguar encarnaciones con mismo slot y versión");

                _ = encarnacion2.IsMetadatoValido(t1).Should().BeFalse();
                _ = encarnacion2.IsMetadatoValido(t2).Should().BeTrue();
            }
            finally
            {
                if (tokenArena2 != 0L)
                    ValueLINQArenaManager.Liberar(tokenArena2);

                foreach (long tok in arenasTemporales)
                    ValueLINQArenaManager.Liberar(tok);
            }
        }

        [Fact]
        public void UnTokenDeOtraArenaSeRechazaAunqueElSlotCoincida()
        {
            long tokenArena1 = ValueLINQArenaManager.Alquilar();
            long tokenArena2 = ValueLINQArenaManager.Alquilar();
            int id1 = TokenHelper.ObtenerIdTokenArena(tokenArena1);
            int id2 = TokenHelper.ObtenerIdTokenArena(tokenArena2);
            long gen1 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena1);
            long gen2 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena2);
            try
            {
                TablaSesiones<int> tablaArena1 = new(id1, gen1);
                TablaSesiones<int> tablaArena2 = new(id2, gen2);
                long tokenArena1Sesion = tablaArena1.ObtenerMetadatos(4).Token;
                _ = tablaArena2.ObtenerMetadatos(4).Token;

                Action presentarloEnTablaAjena = () => _ = tablaArena2.ObtenerMetadatos(tokenArena1Sesion).IsDisposed;

                _ = tablaArena2.IsMetadatoValido(tokenArena1Sesion).Should().BeFalse();
                _ = presentarloEnTablaAjena.Should().Throw<ValueLinqSesionExpiradaException>();
                _ = tablaArena1.IsMetadatoValido(tokenArena1Sesion).Should().BeTrue();
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArena1);
                ValueLINQArenaManager.Liberar(tokenArena2);
            }
        }

        [Fact]
        public void LiberarTodoDevuelveTodasLasSesionesYVaciaLaTabla()
        {
            TablaSesiones<int> tabla = new(0);
            long[] tokens = new long[200];
            for (int i = 0; i < tokens.Length; i++)
                tokens[i] = tabla.ObtenerMetadatos(4).Token;

            tabla.HasSesionesVivas.Should().BeTrue();

            tabla.LiberarTodo();

            tabla.HasSesionesVivas.Should().BeFalse();
            tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots);
            foreach (long token in tokens)
                tabla.IsMetadatoValido(token).Should().BeFalse();
        }

        [Fact]
        public void LiberarTodoAtraviesaTodasLasParticionesMaterializadas()
        {
            TablaSesiones<int> tabla = new(0);
            for (int i = 0; i < ValueLINQConfig.SlotsEnParticion * 3; i++)
                _ = tabla.ObtenerMetadatos(2);

            tabla.LiberarTodo();

            tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots);
        }

        [Fact]
        public void FalloAlAlquilarElBufferDevuelveElIndiceALaPila()
        {
            TablaSesiones<int> tabla = new(0);
            int libresAntes = tabla.IndicesLibres;

            Action obtenerConTamañoInvalido = () => tabla.ObtenerMetadatos(-1);

            obtenerConTamañoInvalido.Should().Throw<ArgumentOutOfRangeException>();
            tabla.IndicesLibres.Should().Be(libresAntes);
            tabla.HasSesionesVivas.Should().BeFalse();
        }

        [Fact]
        public void FalloAlAlquilarElBufferNoAgotaLaCapacidadDeLaTabla()
        {
            TablaSesiones<int> tabla = new(0);

            for (int i = 0; i < ValueLINQConfig.Slots + 10; i++)
            {
                Action obtenerConTamañoInvalido = () => tabla.ObtenerMetadatos(-1);
                obtenerConTamañoInvalido.Should().Throw<ArgumentOutOfRangeException>();
            }

            tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots);
            tabla.ObtenerMetadatos(4).Token.Should().NotBe(0L);
        }

        [Fact]
        public void HasSesionesVivasReflejaElEstadoDeLaTabla()
        {
            TablaSesiones<int> tabla = new(0);
            tabla.HasSesionesVivas.Should().BeFalse();

            long token = tabla.ObtenerMetadatos(4).Token;
            tabla.HasSesionesVivas.Should().BeTrue();

            tabla.LiberarMetadatos(token);
            tabla.HasSesionesVivas.Should().BeFalse();
        }

        [Fact]
        public void LiberarTokensInvalidosEsInofensivo()
        {
            TablaSesiones<int> tabla = new(0);
            _ = tabla.ObtenerMetadatos(1);

            Action liberarCero = () => tabla.LiberarMetadatos(0L);
            Action liberarParticionInexistente = () => tabla.LiberarMetadatos(TokenHelper.CrearToken(0, 0, 1L));
            Action liberarSlotVirgen = () => tabla.LiberarMetadatos(TokenHelper.CrearToken(ValueLINQConfig.Slots - ValueLINQConfig.SlotsEnParticion, 0, 1L));

            _ = liberarCero.Should().NotThrow();
            _ = liberarParticionInexistente.Should().NotThrow();
            _ = liberarSlotVirgen.Should().NotThrow();
        }

        [Fact]
        public void ObtenerMetadatosEnArenaInactivaLanzaArenaInactivaYDevuelveSlotALaPila()
        {
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
            long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
            TablaSesiones<int> tabla = new(idArena, genArena);

            ValueLINQArenaManager.Liberar(tokenArena);

            int slotsLibresAntes = tabla.IndicesLibres;

            Action accion = () => _ = tabla.ObtenerMetadatos(4);

            _ = accion.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idArena);

            _ = tabla.IndicesLibres.Should().Be(slotsLibresAntes,
                "el índice extraído debe reintegrarse inmediatamente a la pila sin fugas");
            _ = tabla.HasSesionesVivas.Should().BeFalse(
                "ninguna sesión debe quedar registrada como viva en una arena inactiva");
        }

        [Fact]
        public void ObtenerMetadatosEnArenaInactivaRepetidamenteNoAgotaCapacidad()
        {
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
            long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
            TablaSesiones<int> tabla = new(idArena, genArena);

            ValueLINQArenaManager.Liberar(tokenArena);

            const int intentos = ValueLINQConfig.Slots + 50;

            for (int i = 0; i < intentos; i++)
            {
                Action accion = () => _ = tabla.ObtenerMetadatos(4);
                _ = accion.Should().Throw<ValueLinqArenaInactivaException>();
            }

            _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots,
                "la totalidad de los índices debe permanecer disponible en la pila tras rechazos de arena inactiva");
            _ = tabla.HasSesionesVivas.Should().BeFalse();
        }

        [Fact]
        public void LiberarMetadatosTrasLiberacionDeArenaEsInofensivoEIdempotente()
        {
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
            long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
            TablaSesiones<int> tabla = new(idArena, genArena);

            ref MetadatosSesion<int> metadato = ref tabla.ObtenerMetadatos(16);
            long tokenSesion = metadato.Token;

            _ = tabla.IsMetadatoValido(tokenSesion).Should().BeTrue();

            ValueLINQArenaManager.Liberar(tokenArena);
            tabla.LiberarTodo();

            _ = tabla.IsMetadatoValido(tokenSesion).Should().BeFalse();

            Action primeraLiberacion = () => tabla.LiberarMetadatos(tokenSesion);
            Action segundaLiberacion = () => tabla.LiberarMetadatos(tokenSesion);

            _ = primeraLiberacion.Should().NotThrow(
                "liberar una sesión cuya arena fue previamente desactivada o barrida debe completarse de forma segura");
            _ = segundaLiberacion.Should().NotThrow(
                "la liberación repetida sobre una sesión ya barrida debe ser estrictamente idempotente");

            _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots);
            _ = tabla.HasSesionesVivas.Should().BeFalse();
        }

        [Fact]
        public async Task ObtenerMetadatosConcurrenteConLiberarTodoEnTablaSesionesMantieneConsistencia()
        {
            const int iteraciones = 30;
            const int hilos = 8;

            for (int iter = 0; iter < iteraciones; iter++)
            {
                long tokenArena = ValueLINQArenaManager.Alquilar();
                int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);
                long genArena = TokenHelper.ObtenerGeneracionTokenArena(tokenArena);
                TablaSesiones<int> tabla = new(idArena, genArena);

                using Barrier barrera = new(hilos + 1);
                ConcurrentBag<Exception> excepcionesInesperadas = [];
                Task[] tareas = new Task[hilos + 1];

                for (int t = 0; t < hilos; t++)
                {
                    tareas[t] = Task.Run(() =>
                    {
                        barrera.SignalAndWait();
                        for (int i = 0; i < 25; i++)
                        {
                            long tokenSesion = 0L;
                            try
                            {
                                ref MetadatosSesion<int> metadato = ref tabla.ObtenerMetadatos(4);
                                tokenSesion = metadato.Token;
                            }
                            catch (ValueLinqArenaInactivaException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                excepcionesInesperadas.Add(ex);
                                break;
                            }
                            finally
                            {
                                if (tokenSesion != 0L)
                                    tabla.LiberarMetadatos(tokenSesion);
                            }
                        }
                    });
                }

                tareas[hilos] = Task.Run(async () =>
                {
                    barrera.SignalAndWait();
                    await Task.Yield();
                    ValueLINQArenaManager.Liberar(tokenArena);
                    tabla.LiberarTodo();
                });

                await Task.WhenAll(tareas);

                _ = excepcionesInesperadas.Should().BeEmpty(
                    "la carrera entre adquisición de sesiones y barrido de tabla no debe arrojar excepciones no controladas");
                _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots,
                    "tras completar la liberación y devolver todas las sesiones, el stack debe recuperar su capacidad íntegra");
                _ = tabla.HasSesionesVivas.Should().BeFalse();
            }
        }
    }
}
