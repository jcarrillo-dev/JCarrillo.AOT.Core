using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class ArenaManagerTests
    {
        private readonly struct TipoArenaCeroManager { }

        [Fact]
        public void AlquilarProduceArenasVivasConIdsUnicos()
        {
            long tokenA = ValueLINQArenaManager.Alquilar();
            long tokenB = ValueLINQArenaManager.Alquilar();
            try
            {
                _ = TokenHelper.ObtenerIdTokenArena(tokenA).Should().NotBe(0);
                _ = TokenHelper.ObtenerIdTokenArena(tokenB).Should().NotBe(0);
                _ = TokenHelper.ObtenerIdTokenArena(tokenA).Should().NotBe(TokenHelper.ObtenerIdTokenArena(tokenB));
                _ = ValueLINQArenaManager.IsArenaViva(tokenA).Should().BeTrue();
                _ = ValueLINQArenaManager.IsArenaViva(tokenB).Should().BeTrue();
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenA);
                ValueLINQArenaManager.Liberar(tokenB);
            }
        }

        [Fact]
        public void LiberarMataElHandleYEsIdempotente()
        {
            long token = ValueLINQArenaManager.Alquilar();

            ValueLINQArenaManager.Liberar(token);
            ValueLINQArenaManager.Liberar(token);

            _ = ValueLINQArenaManager.IsArenaViva(token).Should().BeFalse();

            long tokenA = ValueLINQArenaManager.Alquilar();
            long tokenB = ValueLINQArenaManager.Alquilar();
            try
            {
                _ = TokenHelper.ObtenerIdTokenArena(tokenA).Should().NotBe(TokenHelper.ObtenerIdTokenArena(tokenB),
                    "una doble liberación no debe devolver el mismo id dos veces al stack");
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenA);
                ValueLINQArenaManager.Liberar(tokenB);
            }
        }

        [Fact]
        public void LiberarInvalidaElTokenYAlquilarDaUnoNuevoVivo()
        {
            long tokenViejo = ValueLINQArenaManager.Alquilar();
            ValueLINQArenaManager.Liberar(tokenViejo);

            _ = ValueLINQArenaManager.IsArenaViva(tokenViejo).Should().BeFalse();

            long tokenNuevo = ValueLINQArenaManager.Alquilar();
            try
            {
                _ = ValueLINQArenaManager.IsArenaViva(tokenNuevo).Should().BeTrue();
                _ = tokenNuevo.Should().NotBe(tokenViejo);
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenNuevo);
            }
        }

        [Fact]
        public void ElRecicladoDeIdsEsFifoNoLifo()
        {
            long tokenA = ValueLINQArenaManager.Alquilar();
            int idA = TokenHelper.ObtenerIdTokenArena(tokenA);
            ValueLINQArenaManager.Liberar(tokenA);

            long tokenB = ValueLINQArenaManager.Alquilar();
            try
            {
                _ = TokenHelper.ObtenerIdTokenArena(tokenB).Should().NotBe(idA,
                    "FIFO no debe reutilizar el id recién liberado de inmediato");
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenB);
            }
        }

        [Fact]
        public void LaArenaCeroNoSePuedeLiberar()
        {
            long tokenForjado = TokenHelper.CrearTokenArena(0, 1L);

            Action liberar = () => ValueLINQArenaManager.Liberar(tokenForjado);

            _ = liberar.Should().NotThrow();
            _ = ValueLINQArenaManager.IsArenaViva(tokenForjado).Should().BeTrue("la arena ambiente es imposible de liberar");

            long tokenSesion = ValueLINQStateManager<TipoArenaCeroManager>.ObtenerMetadatos(4).Token;
            _ = ValueLINQStateManager<TipoArenaCeroManager>.IsMetadatoValido(tokenSesion).Should().BeTrue();
        }

        [Fact]
        public void LiberarInvocaLosLiberadoresConElHandleYaMuerto()
        {
            List<int> idsRecibidos = [];
            List<bool> vivaDuranteElCallback = [];
            long token = ValueLINQArenaManager.Alquilar();
            int id = TokenHelper.ObtenerIdTokenArena(token);

            ValueLINQArenaManager.Registrar(
                idRecibido =>
                {
                    if (idRecibido == id)
                    {
                        idsRecibidos.Add(idRecibido);
                        vivaDuranteElCallback.Add(ValueLINQArenaManager.IsArenaViva(token));
                    }
                },
                _ => false);

            ValueLINQArenaManager.Liberar(token);

            _ = idsRecibidos.Should().Equal(id);
            _ = vivaDuranteElCallback.Should().Equal(false);
        }

        [Fact]
        public void AgotarLaCapacidadLanzaYSeRecuperaAlLiberar()
        {
            List<long> tokens = [];
            bool HasLanzado = false;

            try
            {
                try
                {
                    for (int i = 0; i <= ValueLINQConfig.Arenas; i++)
                        tokens.Add(ValueLINQArenaManager.Alquilar());
                }
                catch (InvalidOperationException)
                {
                    HasLanzado = true;
                }

                _ = HasLanzado.Should().BeTrue();
                _ = tokens.Count.Should().BeGreaterThan(ValueLINQConfig.Arenas - 100,
                    "casi todos los ids deben haberse podido alquilar");
            }
            finally
            {
                foreach (long token in tokens)
                    ValueLINQArenaManager.Liberar(token);
            }

            long recuperada = ValueLINQArenaManager.Alquilar();
            try
            {
                _ = ValueLINQArenaManager.IsArenaViva(recuperada).Should().BeTrue();
            }
            finally
            {
                ValueLINQArenaManager.Liberar(recuperada);
            }
        }
    }
}
