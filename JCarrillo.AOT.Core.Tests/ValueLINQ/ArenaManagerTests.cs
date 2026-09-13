using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
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

        [Fact]
        public void RecolectarArenasReentranciaEnMismoHiloRetornaCeroInmediatamente()
        {
            long token = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(token);
            int resultadoReentrante = -1;
            int contadorEjecucion = 0;
            bool isActivo = true;

            ValueLINQArenaManager.Registrar(
                _ => { },
                id =>
                {
                    if (isActivo && id == idArena && Interlocked.Increment(ref contadorEjecucion) == 1)
                        resultadoReentrante = ValueLINQArenaManager.RecolectarArenas();

                    return default;
                });

            try
            {
                int resultadoExterno = ValueLINQArenaManager.RecolectarArenas();

                _ = resultadoReentrante.Should().Be(0, "una invocación anidada a RecolectarArenas debe retornar 0 de inmediato");
                _ = resultadoExterno.Should().BeGreaterThanOrEqualTo(0);
            }
            finally
            {
                isActivo = false;
                ValueLINQArenaManager.Liberar(token);
            }
        }

        [Fact]
        public async Task RecolectarArenasConcurrenteDuranteEjecucionRetornaCeroInmediatamente()
        {
            long token = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(token);
            using ManualResetEventSlim enRecoleccion = new(false);
            using ManualResetEventSlim reentranciaVerificada = new(false);
            int resultadoConcurrente = -1;
            bool isActivo = true;

            ValueLINQArenaManager.Registrar(
                _ => { },
                id =>
                {
                    if (isActivo && id == idArena)
                    {
                        enRecoleccion.Set();
                        _ = reentranciaVerificada.Wait(TimeSpan.FromSeconds(5));
                    }

                    return default;
                });

            Task<int>? tareaRecoleccion = null;
            try
            {
                tareaRecoleccion = Task.Run(() => ValueLINQArenaManager.RecolectarArenas());

                bool hasIngresado = enRecoleccion.Wait(TimeSpan.FromSeconds(5));
                _ = hasIngresado.Should().BeTrue("el hilo en segundo plano debe haber ingresado al barrido");

                Stopwatch cronometro = Stopwatch.StartNew();
                resultadoConcurrente = ValueLINQArenaManager.RecolectarArenas();
                cronometro.Stop();

                reentranciaVerificada.Set();

                _ = resultadoConcurrente.Should().Be(0, "la llamada concurrente debe abortar y retornar 0 de inmediato");
                _ = cronometro.ElapsedMilliseconds.Should().BeLessThan(2000, "el retorno debe ser inmediato sin esperas activas");

                int resultadoFondo = await tareaRecoleccion;
                _ = resultadoFondo.Should().BeGreaterThanOrEqualTo(0);
            }
            finally
            {
                isActivo = false;
                reentranciaVerificada.Set();
                if (tareaRecoleccion is not null)
                    _ = await Task.WhenAny(tareaRecoleccion, Task.Delay(TimeSpan.FromSeconds(5)));

                ValueLINQArenaManager.Liberar(token);
            }
        }

        [Fact]
        public async Task RecolectarArenasConcurrenciaMasivaSinBloqueosNiExcepciones()
        {
            const int hilos = 16;
            const int iteracionesPorHilo = 50;

            List<ValueLINQArena> arenasCreadas = [];
            for (int i = 0; i < 8; i++)
                arenasCreadas.Add(ValueLINQArena.Crear(inactividad: TimeSpan.Zero));

            using Barrier barrera = new(hilos);
            ConcurrentBag<Exception> excepciones = new();
            Task[] tareas = new Task[hilos];

            for (int i = 0; i < hilos; i++)
                tareas[i] = Task.Run(() =>
                {
                    try
                    {
                        barrera.SignalAndWait();

                        for (int it = 0; it < iteracionesPorHilo; it++)
                        {
                            int recolectadas = ValueLINQArenaManager.RecolectarArenas();
                            if (recolectadas < 0)
                                throw new InvalidOperationException($"Conteo negativo de recolección: {recolectadas}");
                        }
                    }
                    catch (Exception ex)
                    {
                        excepciones.Add(ex);
                    }
                });

            Task tareaTodas = Task.WhenAll(tareas);
            Task tareaCompletada = await Task.WhenAny(tareaTodas, Task.Delay(TimeSpan.FromSeconds(30)));
            bool hasCompletado = tareaCompletada == tareaTodas;

            foreach (ValueLINQArena arena in arenasCreadas)
                if (arena.IsViva)
                    arena.Dispose();

            _ = hasCompletado.Should().BeTrue("todas las tareas deben completarse sin interbloqueos en el tiempo límite");
            _ = excepciones.Should().BeEmpty("ningún hilo debe experimentar excepciones durante la contención concurrente masiva");

            int recoleccionPosterior = ValueLINQArenaManager.RecolectarArenas();
            _ = recoleccionPosterior.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void RecolectarArenasBloqueFinallyRestauraGuardaParaLlamadasPosteriores()
        {
            ValueLINQArena primeraArena = ValueLINQArena.Crear(inactividad: TimeSpan.Zero);
            _ = primeraArena.IsViva.Should().BeTrue();

            int primeraRecoleccion = ValueLINQArenaManager.RecolectarArenas();
            _ = primeraRecoleccion.Should().BeGreaterThan(0);
            _ = primeraArena.IsViva.Should().BeFalse();

            FieldInfo? campoGuarda = typeof(ValueLINQArenaManager).GetField("_isRecolectando", BindingFlags.NonPublic | BindingFlags.Static);
            _ = campoGuarda.Should().NotBeNull("el campo _isRecolectando debe existir en ValueLINQArenaManager");
            _ = ((int)campoGuarda!.GetValue(null)!).Should().Be(0, "el bloque finally debe dejar la guarda en 0 tras completar");

            ValueLINQArena segundaArena = ValueLINQArena.Crear(inactividad: TimeSpan.Zero);
            _ = segundaArena.IsViva.Should().BeTrue();

            int segundaRecoleccion = ValueLINQArenaManager.RecolectarArenas();
            _ = segundaRecoleccion.Should().BeGreaterThan(0, "la guarda liberada debe permitir que la siguiente llamada recolecte");
            _ = segundaArena.IsViva.Should().BeFalse();

            _ = ((int)campoGuarda.GetValue(null)!).Should().Be(0, "la guarda debe retornar a 0 tras la segunda recolección");
        }

        [Fact]
        public void RecolectarArenasRecuperaGuardaTrasExcepcionEnColeccion()
        {
            long token = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(token);
            int lanzamientos = 0;
            bool isActivo = true;

            ValueLINQArenaManager.Registrar(
                _ => { },
                id =>
                {
                    if (isActivo && id == idArena && Interlocked.Increment(ref lanzamientos) == 1)
                        throw new InvalidOperationException("Fallo simulado para verificar recuperación de excepción.");

                    return default;
                });

            try
            {
                Action accionFallida = () => ValueLINQArenaManager.RecolectarArenas();
                _ = accionFallida.Should().Throw<InvalidOperationException>();

                FieldInfo? campoGuarda = typeof(ValueLINQArenaManager).GetField("_isRecolectando", BindingFlags.NonPublic | BindingFlags.Static);
                _ = campoGuarda.Should().NotBeNull("el campo _isRecolectando debe existir en ValueLINQArenaManager");
                _ = ((int)campoGuarda!.GetValue(null)!).Should().Be(0, "la guarda debe quedar en 0 tras lanzar excepción");

                ValueLINQArena arenaPosterior = ValueLINQArena.Crear(inactividad: TimeSpan.Zero);
                int recolectadas = ValueLINQArenaManager.RecolectarArenas();

                _ = recolectadas.Should().BeGreaterThan(0, "la siguiente llamada debe recolectar con normalidad tras una excepción previa");
                _ = arenaPosterior.IsViva.Should().BeFalse();
            }
            finally
            {
                isActivo = false;
                ValueLINQArenaManager.Liberar(token);
            }
        }

        [Fact]
        public async Task RecolectarArenasContencionRetornaCeroConCeroAsignacionesHeap()
        {
            long token = ValueLINQArenaManager.Alquilar();
            int idArena = TokenHelper.ObtenerIdTokenArena(token);
            using ManualResetEventSlim enRecoleccion = new(false);
            using ManualResetEventSlim reentranciaVerificada = new(false);
            bool isActivo = true;

            ValueLINQArenaManager.Registrar(
                _ => { },
                id =>
                {
                    if (isActivo && id == idArena)
                    {
                        enRecoleccion.Set();
                        _ = reentranciaVerificada.Wait(TimeSpan.FromSeconds(5));
                    }

                    return default;
                });

            Task<int>? tareaRecoleccion = null;
            try
            {
                tareaRecoleccion = Task.Run(() => ValueLINQArenaManager.RecolectarArenas());

                bool hasIngresado = enRecoleccion.Wait(TimeSpan.FromSeconds(5));
                _ = hasIngresado.Should().BeTrue();

                // Calentamiento JIT
                _ = ValueLINQArenaManager.RecolectarArenas();

                long asignadoAntes = GC.GetAllocatedBytesForCurrentThread();
                int resultado = ValueLINQArenaManager.RecolectarArenas();
                long asignadoDespues = GC.GetAllocatedBytesForCurrentThread();

                reentranciaVerificada.Set();

                _ = resultado.Should().Be(0);
                _ = (asignadoDespues - asignadoAntes).Should().Be(0, "la salida anticipada por contención no debe realizar asignaciones en el heap");

                _ = await tareaRecoleccion;
            }
            finally
            {
                isActivo = false;
                reentranciaVerificada.Set();
                if (tareaRecoleccion is not null)
                    _ = await Task.WhenAny(tareaRecoleccion, Task.Delay(TimeSpan.FromSeconds(5)));

                ValueLINQArenaManager.Liberar(token);
            }
        }
    }
}
