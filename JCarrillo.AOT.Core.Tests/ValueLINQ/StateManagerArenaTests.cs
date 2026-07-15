using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
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
        public void CrearTablaConcurrenteConLiberacionDeArenaNoDejaTablaHuerfana()
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

                Task.WaitAll(tareas);

                _ = ValueLINQArenaManager.IsArenaViva(tokenArena).Should().BeFalse();
                _ = ValueLINQStateManager<TipoRaceCas>.IsTablaMaterializada(id).Should().BeFalse(
                    "tras liberar la arena no debe quedar ninguna tabla huérfana para ese id");
            }
        }

        [Fact]
        public void CreacionConcurrenteDeUnaArenaSoloMaterializaUnaTabla()
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

                Task.WaitAll(tareas);

                _ = tokens.Should().OnlyContain(token => ValueLINQStateManager<TipoCas>.IsMetadatoValido(token));
                _ = tokens.Should().OnlyContain(token => TokenHelper.ObtenerArenaId(token) == idArena);
                _ = tokens.Select(TokenHelper.ObtenerSlotIndex).Distinct().Should().HaveCount(hilos);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArenaManager);
            }
        }
    }
}
