using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Collections.Concurrent;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class StateManagerArenaTests
    {
        private readonly struct TipoVirgenTolerante { }
        private readonly struct TipoArenaCero { }
        private readonly struct TipoForjado { }
        private readonly struct TipoCas { }
        private readonly struct TipoEnrutado { }
        private readonly struct TipoRaceCas { }

        [Fact]
        public void TipoVirgenNoLanzaEnCaminosTolerantes()
        {
            Action liberar = () => ValueLINQStateManager<TipoVirgenTolerante>.LiberarMetadatos(0L);
            Action refrescar = () => ValueLINQStateManager<TipoVirgenTolerante>.RefrescarUltimoAcceso(0L, TimeSpan.Zero);

            _ = liberar.Should().NotThrow();
            _ = refrescar.Should().NotThrow();
            _ = ValueLINQStateManager<TipoVirgenTolerante>.IsMetadatoValido(0L).Should().BeFalse();
        }

        [Fact]
        public void LaArenaCeroNaceEagerConElTipo()
        {
            _ = ValueLINQStateManager<TipoArenaCero>.IsTablaMaterializada(0).Should().BeTrue();
            _ = ValueLINQStateManager<TipoArenaCero>.SlotsLibres.Should().Be(ValueLINQConfig.Slots);
        }

        [Fact]
        public void TokenDeArenaJamasUsadaSeToleraSinMaterializarNada()
        {
            long forjado = TokenHelper.CrearToken(7, 5, 1L);

            Action liberar = () => ValueLINQStateManager<TipoForjado>.LiberarMetadatos(forjado);
            Action refrescar = () => ValueLINQStateManager<TipoForjado>.RefrescarUltimoAcceso(forjado, TimeSpan.Zero);
            Action trabajar = () => ValueLINQStateManager<TipoForjado>.Añadir(forjado, default(TipoForjado));

            _ = liberar.Should().NotThrow();
            _ = refrescar.Should().NotThrow();
            _ = ValueLINQStateManager<TipoForjado>.IsMetadatoValido(forjado).Should().BeFalse();
            _ = trabajar.Should().Throw<ValueLinqArenaInactivaException>();
            _ = ValueLINQStateManager<TipoForjado>.IsTablaMaterializada(5).Should().BeFalse();
        }

        [Fact]
        public void CrearSesionEnArenaNoAlquiladaLanzaArenaInactiva()
        {
            long forjado = TokenHelper.CrearTokenArena(9, 1L);
            int idInexistente = TokenHelper.ObtenerIdTokenArena(forjado);

            Action crear = () => _ = ValueLINQStateManager<TipoForjado>.ObtenerMetadatos(idInexistente, 4);

            _ = crear.Should().Throw<ValueLinqArenaInactivaException>();
            _ = ValueLINQStateManager<TipoForjado>.IsTablaMaterializada(idInexistente).Should().BeFalse();
        }

        [Fact]
        public void LosTokensDeArenasDistintasSeEnrutanASusTablas()
        {
            long tokenArenaManager = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArenaManager);
            try
            {
                long tokenArena0 = ValueLINQStateManager<TipoEnrutado>.ObtenerMetadatos(4).Token;
                long tokenArenaN = ValueLINQStateManager<TipoEnrutado>.ObtenerMetadatos(idArena, 4).Token;

                _ = TokenHelper.ObtenerArenaId(tokenArena0).Should().Be(0);
                _ = TokenHelper.ObtenerArenaId(tokenArenaN).Should().Be(idArena);
                _ = TokenHelper.ObtenerSlotIndex(tokenArena0).Should().Be(TokenHelper.ObtenerSlotIndex(tokenArenaN));

                _ = ValueLINQStateManager<TipoEnrutado>.IsMetadatoValido(tokenArena0).Should().BeTrue();
                _ = ValueLINQStateManager<TipoEnrutado>.IsMetadatoValido(tokenArenaN).Should().BeTrue();

                ValueLINQStateManager<TipoEnrutado>.LiberarMetadatos(tokenArenaN);

                _ = ValueLINQStateManager<TipoEnrutado>.IsMetadatoValido(tokenArenaN).Should().BeFalse();
                _ = ValueLINQStateManager<TipoEnrutado>.IsMetadatoValido(tokenArena0).Should().BeTrue();
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArenaManager);
            }
        }

        [Fact]
        public async Task CrearTablaConcurrenteConLiberacionDeArenaNoDejaTablaHuerfana()
        {
            const int iteraciones = 60;
            const int creadores = 6;

            for (int iter = 0; iter < iteraciones; iter++)
            {
                long tokenArena = ValueLINQArenaManager.Alquilar();
                int id = TokenHelper.ObtenerIdTokenArena(tokenArena);

                using Barrier barrera = new(creadores + 1);
                Task[] tareas = new Task[creadores + 1];

                for (int t = 0; t < creadores; t++)
                {
                    tareas[t] = Task.Run(() =>
                    {
                        barrera.SignalAndWait();
                        try
                        {
                            _ = ValueLINQStateManager<TipoRaceCas>.ObtenerMetadatos(id, 4).Token;
                        }
                        catch (ValueLinqArenaInactivaException)
                        {
                            // Esperado si la arena murió antes de crear la sesión.
                        }
                    });
                }

                tareas[creadores] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    ValueLINQArenaManager.Liberar(tokenArena);
                });

                await Task.WhenAll(tareas);

                _ = ValueLINQArenaManager.IsArenaViva(tokenArena).Should().BeFalse();
                _ = ValueLINQStateManager<TipoRaceCas>.IsTablaMaterializada(id).Should().BeFalse(
                    "tras liberar la arena no debe quedar ninguna tabla huérfana para ese id");
            }
        }

        [Fact]
        public async Task CreacionConcurrenteDeUnaArenaSoloMaterializaUnaTabla()
        {
            long tokenArenaManager = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArenaManager);
            try
            {
                const int hilos = 16;
                using Barrier barrera = new(hilos);
                long[] tokens = new long[hilos];

                Task[] tareas = new Task[hilos];
                for (int t = 0; t < hilos; t++)
                {
                    int i = t;
                    tareas[t] = Task.Run(() =>
                    {
                        barrera.SignalAndWait();
                        tokens[i] = ValueLINQStateManager<TipoCas>.ObtenerMetadatos(idArena, 4).Token;
                    });
                }

                await Task.WhenAll(tareas);

                _ = tokens.Should().OnlyContain(token => ValueLINQStateManager<TipoCas>.IsMetadatoValido(token));
                _ = tokens.Should().OnlyContain(token => TokenHelper.ObtenerArenaId(token) == idArena);
                _ = tokens.Select(TokenHelper.ObtenerSlotIndex).Distinct().Should().HaveCount(hilos);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArenaManager);
            }
        }

        [Fact]
        public void ObtenerMetadatosEnStateManagerConArenaDispuestaLanzaArenaInactiva()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            long tokenArena = arena.TokenArena;
            int idArena = arena.Id;

            arena.Dispose();

            Action porToken = () => _ = ValueLINQStateManager<TipoVirgenTolerante>.ObtenerMetadatos(tokenArena, 4);
            Action porId = () => _ = ValueLINQStateManager<TipoVirgenTolerante>.ObtenerMetadatos(idArena, 4);

            _ = porToken.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idArena);
            _ = porId.Should().Throw<ValueLinqArenaInactivaException>()
                .Which.IdArena.Should().Be(idArena);

            _ = ValueLINQStateManager<TipoVirgenTolerante>.IsTablaMaterializada(idArena).Should().BeFalse();
        }

        [Fact]
        public async Task AdquisicionConcurrenteDuranteLiberacionDeArenaNoGeneraFugasNiCorrupcion()
        {
            const int iteraciones = 40;
            const int hilosAdquisicion = 12;

            for (int iter = 0; iter < iteraciones; iter++)
            {
                long tokenArena = ValueLINQArenaManager.Alquilar();
                int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);

                using Barrier barrera = new(hilosAdquisicion + 1);
                ConcurrentBag<Exception> excepcionesInesperadas = [];
                Task[] tareas = new Task[hilosAdquisicion + 1];

                for (int t = 0; t < hilosAdquisicion; t++)
                {
                    int hiloId = t;
                    tareas[t] = Task.Run(() =>
                    {
                        barrera.SignalAndWait();
                        for (int i = 0; i < 20; i++)
                        {
                            long tokenSesion = 0L;
                            try
                            {
                                ref MetadatosSesion<int> metadato = ref ValueLINQStateManager<int>.ObtenerMetadatos(idArena, 8);
                                tokenSesion = metadato.Token;

                                int[]? array = metadato.Array;
                                if (array is not null)
                                {
                                    array[0] = hiloId * 100 + i;
                                    metadato.TamañoActual = 1;
                                }
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
                                    ValueLINQStateManager<int>.LiberarMetadatos(tokenSesion);
                            }
                        }
                    });
                }

                tareas[hilosAdquisicion] = Task.Run(async () =>
                {
                    barrera.SignalAndWait();
                    await Task.Yield();
                    ValueLINQArenaManager.Liberar(tokenArena);
                });

                await Task.WhenAll(tareas);

                _ = excepcionesInesperadas.Should().BeEmpty(
                    "la contención concurrente entre adquisición y liberación de arena solo puede arrojar ValueLinqArenaInactivaException");
                _ = ValueLINQArenaManager.IsArenaViva(tokenArena).Should().BeFalse();
                _ = ValueLINQStateManager<int>.IsTablaMaterializada(idArena).Should().BeFalse();
            }
        }

        [Fact]
        public async Task LiberarMetadatosConcurrenteEnMultiplesSesionesTrasDesactivacionDeArena()
        {
            const int totalSesiones = 32;
            long tokenArena = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(tokenArena);

            long[] tokensSesion = new long[totalSesiones];
            for (int i = 0; i < totalSesiones; i++)
                tokensSesion[i] = ValueLINQStateManager<int>.ObtenerMetadatos(idArena, 8).Token;

            ValueLINQArenaManager.Liberar(tokenArena);

            using Barrier barrera = new(totalSesiones);
            ConcurrentBag<Exception> excepciones = [];
            Task[] tareas = new Task[totalSesiones];

            for (int i = 0; i < totalSesiones; i++)
            {
                int indice = i;
                tareas[i] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    try
                    {
                        ValueLINQStateManager<int>.LiberarMetadatos(tokensSesion[indice]);
                    }
                    catch (Exception ex)
                    {
                        excepciones.Add(ex);
                    }
                });
            }

            await Task.WhenAll(tareas);

            _ = excepciones.Should().BeEmpty(
                "ningún hilo debe experimentar excepciones al liberar sesiones de una arena desactivada");

            foreach (long token in tokensSesion)
                _ = ValueLINQStateManager<int>.IsMetadatoValido(token).Should().BeFalse();
        }
    }
}
