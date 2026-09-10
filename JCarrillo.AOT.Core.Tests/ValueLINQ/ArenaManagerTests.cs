using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
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
        public void ObtenerMetadatosEnUnaArenaLiberadaLanzaEnSuPropiaCapa()
        {
            // Preparar: el StateManager defiende su propia precondición sin confiar en que el llamante la validara.
            // Se comprueba invocándolo directamente, que es el único nivel donde ninguna capa anterior lo enmascara.
            long token = ValueLINQArenaManager.Alquilar();
            int id = TokenHelper.ObtenerIdTokenArena(token);
            ValueLINQArenaManager.Liberar(token);

            // Actuar
            Action actObtener = () => _ = ValueLINQStateManager<TipoArenaCeroManager>.ObtenerMetadatos(id, 4).TamañoActual;

            // Aserción
            _ = actObtener.Should().Throw<ValueLinqArenaInactivaException>(
                "crear una tabla en una arena que ya no existe es un error de la propia operación, la valide quien la valide antes");
        }

        [Fact]
        public void SoloLaSobrecargaPorTokenDistingueUnaEncarnacionReciclada()
        {
            // Preparar: es la diferencia que justifica que ValueLINQChunkDelay compare el token completo en vez del
            // identificador. Agotar los 4095 ids para provocar un reciclaje real no es viable en una prueba, pero la
            // distinción sí se comprueba forjando el token de una generación anterior sobre una arena viva.
            long tokenActual = ValueLINQArenaManager.Alquilar();

            try
            {
                int id = TokenHelper.ObtenerIdTokenArena(tokenActual);
                long generacionActual = TokenHelper.ObtenerGeneracionTokenArena(tokenActual);
                long tokenEncarnacionAnterior = TokenHelper.CrearTokenArena(id, generacionActual - 1);

                // Aserción: por identificador la arena está viva, porque lo está; por token, la encarnación anterior
                // se detecta como ajena. Si ambas dijeran lo mismo, el guardián de ChunkDelay sería redundante.
                _ = ValueLINQArenaManager.IsArenaViva(id).Should().BeTrue();
                _ = ValueLINQArenaManager.IsArenaViva(tokenActual).Should().BeTrue();
                _ = ValueLINQArenaManager.IsArenaViva(tokenEncarnacionAnterior).Should().BeFalse(
                    "solo la comparación del token completo distingue encarnaciones del mismo identificador");
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenActual);
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
                _ => default);

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
