using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.Tests.Diagnostico;
using JCarrillo.AOT.Core.Tests.E2E;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System.Collections.Concurrent;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    [Collection("ArrayPoolDiagnostics")]
    public class ValueLINQArenaPoolingTests
    {
        #region Delegados Auxiliares

        private struct PredicadoPar : IWhereDelegado<int>
        {
            public readonly bool Ejecutar(int item) => (item & 1) == 0;
        }

        private struct SelectorDoble : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        private struct Multiplicador(int factor) : ISelectDelegado<int, int>
        {
            private readonly int _factor = factor;
            public readonly int Ejecutar(int item) => item * _factor;
        }

        private readonly struct TipoAbaAntiCorrupcion { }
        private readonly struct TipoAbaGeneracionAislada { }
        private readonly struct TipoSobrecargaAislada { }

        #endregion

        #region Pruebas de Cero Asignaciones en Estado Estacionario

        [Fact]
        public void CicloArenaEnEstadoEstacionarioAsignaCeroBytesEnElMonton()
        {
            int[] datos = [1, 2, 3, 4, 5, 6, 7, 8];
            int sumaFinal = 0;

            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQArena arena = ValueLINQArena.Crear();
                ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                ValueLINQStruct<int> filtrado = query.Where(new PredicadoPar());
                using ValueLINQStruct<int> proyectado = filtrado.Select<int, SelectorDoble, int>(new SelectorDoble());
                int suma = 0;
                foreach (int v in proyectado)
                    suma += v;
                sumaFinal = suma;
            });

            _ = sumaFinal.Should().Be(40);
        }

        [Fact]
        public void MultiplesCiclosArenaConsecutivosAsignanCeroBytesEnElMonton()
        {
            int[] datos = [10, 20, 30, 40, 50];

            using (ValueLINQArena arenaCalentamiento = ValueLINQArena.Crear())
            {
                using ValueLINQStruct<int> q = datos.ToValueQuery(arenaCalentamiento);
                int sumaCalentamiento = 0;
                foreach (int v in q)
                    sumaCalentamiento += v;
                _ = sumaCalentamiento.Should().Be(150);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            int ultimaSuma = 0;
            long asignadoAntes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 50; i++)
            {
                using ValueLINQArena arena = ValueLINQArena.Crear();
                using ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                int suma = 0;
                foreach (int v in query)
                    suma += v;
                ultimaSuma = suma;
            }
            long asignadoDespues = GC.GetAllocatedBytesForCurrentThread();

            _ = ultimaSuma.Should().Be(150);
            _ = (asignadoDespues - asignadoAntes).Should().Be(0,
                "50 ciclos consecutivos de alquiler, consulta y liberación de arena deben asignar exactamente 0 bytes en el montón");
        }

        [Fact]
        public void CicloArenaConBufferYMaterializacionAsignaCeroBytesEnElMonton()
        {
            int[] datos = [5, 2, 8, 1, 9, 3];
            long rentadosDelta;
            long devueltosDelta;

            using (ValueLINQArena arenaCalentamiento = ValueLINQArena.Crear())
            {
                using ValueLINQStruct<int> q = datos.ToValueQuery(arenaCalentamiento);
                using PooledArray<int> arr = q.ToArray();
                _ = arr.Tamaño.Should().Be(6);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            int ultimoTamaño = 0;
            long asignadoAntes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                using ValueLINQArena arena = ValueLINQArena.Crear();
                using ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                using PooledArray<int> arr = query.ToArray();
                ultimoTamaño = arr.Tamaño;
            }
            long asignadoDespues = GC.GetAllocatedBytesForCurrentThread();

            _ = ultimoTamaño.Should().Be(6);
            _ = (asignadoDespues - asignadoAntes).Should().Be(0,
                "el materializado en arrays agrupados dentro de arenas recicladas debe mantener 0 B en el montón");

            using (ArrayPoolDiagnosticsListener listener = new())
            {
                InstantaneaArrayPool inicio = listener.ObtenerInstantanea();

                for (int i = 0; i < 10; i++)
                {
                    using ValueLINQArena arena = ValueLINQArena.Crear();
                    using ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                    using PooledArray<int> arr = query.ToArray();
                    if (arr.Tamaño != 6)
                        throw new InvalidOperationException("Materialización inválida.");
                }

                InstantaneaArrayPool fin = listener.ObtenerInstantanea();
                rentadosDelta = fin.Rentados - inicio.Rentados;
                devueltosDelta = fin.Devueltos - inicio.Devueltos;
            }

            _ = rentadosDelta.Should().BeGreaterThan(0, "debio ocurrir al menos un alquiler fisico en ArrayPool durante los ciclos con reciclaje");
            _ = devueltosDelta.Should().Be(rentadosDelta, "todos los buffers fisicos alquilados durante los ciclos de reciclaje deben devolverse simetricamente");
        }

        #endregion

        #region Pruebas de Concurrencia y Estres Multihilo

        [Fact]
        public async Task ContencionMultihiloAlquilerYConsultaConcurrenteSinBloqueoNiCorrupcion()
        {
            const int hilos = 32;
            const int iteracionesPorHilo = 40;

            using Barrier barrera = new(hilos);
            ConcurrentBag<Exception> excepciones = [];
            Task[] tareas = new Task[hilos];

            for (int t = 0; t < hilos; t++)
            {
                int hiloId = t + 1;
                tareas[t] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    int[] datos = [hiloId, hiloId * 2, hiloId * 3];

                    for (int i = 0; i < iteracionesPorHilo; i++)
                    {
                        try
                        {
                            using ValueLINQArena arena = ValueLINQArena.Crear();
                            using ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                            using ValueLINQStruct<int> proyectado = query.Select<int, Multiplicador, int>(new Multiplicador(2));
                            int suma = 0;
                            foreach (int v in proyectado)
                                suma += v;

                            int sumaEsperada = (hiloId + hiloId * 2 + hiloId * 3) * 2;
                            if (suma != sumaEsperada)
                                throw new InvalidOperationException(
                                    $"Corrupción de datos en hilo {hiloId}: esperada {sumaEsperada}, obtenida {suma}");
                        }
                        catch (Exception ex)
                        {
                            excepciones.Add(ex);
                            break;
                        }
                    }
                });
            }

            Task completada = Task.WhenAll(tareas);
            Task timeout = Task.Delay(TimeSpan.FromSeconds(30));

            Task terminada = await Task.WhenAny(completada, timeout);
            _ = (terminada == completada).Should().BeTrue("todos los hilos concurrentes deben finalizar sin interbloqueos");

            _ = excepciones.Should().BeEmpty("ningún hilo debe experimentar excepciones o corrupción en operaciones concurrentes");
        }

        [Fact]
        public async Task PoolTablasNoSuperaCapacidadAcotadaDeTreintaYDosBajoSobrecarga()
        {
            ValueLINQStateManager<TipoSobrecargaAislada>.VaciarPool();

            const int totalArenas = 48;
            ValueLINQArena[] arenas = new ValueLINQArena[totalArenas];
            using Barrier barrera = new(totalArenas);
            Task[] tareasAlquiler = new Task[totalArenas];

            for (int i = 0; i < totalArenas; i++)
            {
                int indice = i;
                tareasAlquiler[i] = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    arenas[indice] = ValueLINQArena.Crear();
                    _ = ValueLINQStateManager<TipoSobrecargaAislada>.ObtenerMetadatos(arenas[indice].TokenArena, 4);
                });
            }

            await Task.WhenAll(tareasAlquiler);

            using Barrier barreraLiberacion = new(totalArenas);
            Task[] tareasLiberacion = new Task[totalArenas];

            for (int i = 0; i < totalArenas; i++)
            {
                int indice = i;
                tareasLiberacion[i] = Task.Run(() =>
                {
                    barreraLiberacion.SignalAndWait();
                    arenas[indice].Dispose();
                });
            }

            await Task.WhenAll(tareasLiberacion);

            _ = ValueLINQStateManager<TipoSobrecargaAislada>.TablasEnPool.Should().Be(32,
                "el pool acotado de tablas debe saturar en exactamente 32 instancias y descartar el exceso a recoleccion de basura");
        }

        [Fact]
        public async Task ReciclajeConcurrenteEntreHilosCruzadosNoMezclaSesiones()
        {
            const int rondas = 20;
            ConcurrentBag<Exception> errores = [];

            for (int r = 0; r < rondas; r++)
            {
                using Barrier barrera = new(2);

                Task productor = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    using ValueLINQArena arenaA = ValueLINQArena.Crear();
                    int[] datosA = [111, 222, 333];
                    using ValueLINQStruct<int> qA = datosA.ToValueQuery(arenaA);
                    int sumA = 0;
                    foreach (int v in qA)
                        sumA += v;
                    if (sumA != 666)
                        errores.Add(new InvalidOperationException($"Fallo productor ronda {r}: suma {sumA} != 666"));
                });

                Task consumidor = Task.Run(() =>
                {
                    barrera.SignalAndWait();
                    Thread.Yield();
                    using ValueLINQArena arenaB = ValueLINQArena.Crear();
                    int[] datosB = [10, 20, 30];
                    using ValueLINQStruct<int> qB = datosB.ToValueQuery(arenaB);
                    int sumB = 0;
                    foreach (int v in qB)
                        sumB += v;
                    if (sumB != 60)
                        errores.Add(new InvalidOperationException($"Fallo consumidor ronda {r}: suma {sumB} != 60"));
                });

                await Task.WhenAll(productor, consumidor);
            }

            _ = errores.Should().BeEmpty("el reciclaje de tablas entre hilos cruzados no debe mezclar sesiones");
        }

        #endregion

        #region Pruebas de Verificacion Anti-ABA

        [Fact]
        public void ConsultaResidualDeArenaDispuestaNoPuedeInteractuarConTablaReciclada()
        {
            ValueLINQArena arena1 = ValueLINQArena.Crear();
            ref MetadatosSesion<TipoAbaAntiCorrupcion> sesion1 = ref ValueLINQStateManager<TipoAbaAntiCorrupcion>.ObtenerMetadatos(arena1.TokenArena, 4);
            long tokenSesion1 = sesion1.Token;

            _ = ValueLINQStateManager<TipoAbaAntiCorrupcion>.IsMetadatoValido(tokenSesion1).Should().BeTrue();

            arena1.Dispose();
            _ = ValueLINQStateManager<TipoAbaAntiCorrupcion>.IsMetadatoValido(tokenSesion1).Should().BeFalse();

            using ValueLINQArena arena2 = ValueLINQArena.Crear();
            ref MetadatosSesion<TipoAbaAntiCorrupcion> sesion2 = ref ValueLINQStateManager<TipoAbaAntiCorrupcion>.ObtenerMetadatos(arena2.TokenArena, 4);
            long tokenSesion2 = sesion2.Token;

            _ = ValueLINQStateManager<TipoAbaAntiCorrupcion>.IsMetadatoValido(tokenSesion2).Should().BeTrue();

            Action accesoPorTokenResidual = () => _ = ValueLINQStateManager<TipoAbaAntiCorrupcion>.ObtenerMetadatos(tokenSesion1);
            Action añadirPorTokenResidual = () => ValueLINQStateManager<TipoAbaAntiCorrupcion>.Añadir(tokenSesion1, default(TipoAbaAntiCorrupcion));
            Action liberarPorTokenResidual = () => ValueLINQStateManager<TipoAbaAntiCorrupcion>.LiberarMetadatos(tokenSesion1);

            _ = accesoPorTokenResidual.Should().Throw<Exception>();
            _ = añadirPorTokenResidual.Should().Throw<Exception>();
            _ = liberarPorTokenResidual.Should().NotThrow("la liberación tolerante no debe fallar ni afectar a la sesión activa");

            _ = ValueLINQStateManager<TipoAbaAntiCorrupcion>.IsMetadatoValido(tokenSesion2).Should().BeTrue("la sesión activa no debe verse afectada");
        }

        [Fact]
        public void ColisionMismoIdArenaConGeneracionIncrementadaRechazaTokenAntiguo()
        {
            ValueLINQArena arenaOriginal = ValueLINQArena.Crear();
            long tokenArenaOriginal = arenaOriginal.TokenArena;

            ref MetadatosSesion<TipoAbaGeneracionAislada> meta = ref ValueLINQStateManager<TipoAbaGeneracionAislada>.ObtenerMetadatos(tokenArenaOriginal, 4);
            long tokenSesionAntiguo = meta.Token;

            arenaOriginal.Dispose();

            Action accesoConTokenAntiguo = () => _ = ValueLINQStateManager<TipoAbaGeneracionAislada>.ObtenerMetadatos(tokenSesionAntiguo);
            _ = accesoConTokenAntiguo.Should().Throw<ValueLinqArenaInactivaException>(
                "un token de sesion de una generacion previa debe ser rechazado al estar la arena inactiva o con generacion distinta");
        }

        [Fact]
        public void ReiniciarLimpiaCompletamenteMetadatosYRestablecePilaDeIndices()
        {
            long rentadosDelta;
            long devueltosDelta;

            using (ArrayPoolDiagnosticsListener listener = new())
            {
                InstantaneaArrayPool inicio = listener.ObtenerInstantanea();

                long tokenArena1 = ValueLINQArenaManager.Alquilar();
                int idArena1 = TokenHelper.ObtenerIdTokenArena(tokenArena1);
                long gen1 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena1);

                try
                {
                    TablaSesiones<int> tabla = new(idArena1, gen1);

                    long[] tokens = new long[10];
                    for (int i = 0; i < 10; i++)
                        tokens[i] = tabla.ObtenerMetadatos(8).Token;

                    _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots - 10);
                    _ = tabla.HasSesionesVivas.Should().BeTrue();

                    tabla.LiberarTodo();

                    long tokenArena2 = ValueLINQArenaManager.Alquilar();
                    int idArena2 = TokenHelper.ObtenerIdTokenArena(tokenArena2);
                    long gen2 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena2);

                    try
                    {
                        tabla.Reiniciar(idArena2, gen2);

                        _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots, "la pila de índices debe restablecerse a la capacidad total");
                        _ = tabla.HasSesionesVivas.Should().BeFalse("no debe haber sesiones vivas tras reiniciar");
                        _ = tabla.ArenaGen.Should().Be(gen2, "la generación debe actualizarse a la de la nueva arena");

                        foreach (long tokenAntiguo in tokens)
                            _ = tabla.IsMetadatoValido(tokenAntiguo).Should().BeFalse();

                        // Alquilar slots en la tabla reiniciada y ejecutar Reiniciar directamente sin LiberarTodo
                        // para certificar que Reiniciar devuelve físicamente los buffers remanentes a ArrayPool
                        long[] tokensNuevos = new long[5];
                        for (int i = 0; i < 5; i++)
                            tokensNuevos[i] = tabla.ObtenerMetadatos(16).Token;

                        _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots - 5);

                        long tokenArena3 = ValueLINQArenaManager.Alquilar();
                        int idArena3 = TokenHelper.ObtenerIdTokenArena(tokenArena3);
                        long gen3 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena3);

                        try
                        {
                            tabla.Reiniciar(idArena3, gen3);
                            _ = tabla.IndicesLibres.Should().Be(ValueLINQConfig.Slots, "la pila de índices debe restablecerse nuevamente");
                            _ = tabla.HasSesionesVivas.Should().BeFalse("no debe haber sesiones vivas tras reiniciar directamente");
                        }
                        finally
                        {
                            ValueLINQArenaManager.Liberar(tokenArena3);
                        }
                    }
                    finally
                    {
                        ValueLINQArenaManager.Liberar(tokenArena2);
                    }
                }
                finally
                {
                    ValueLINQArenaManager.Liberar(tokenArena1);
                }

                InstantaneaArrayPool fin = listener.ObtenerInstantanea();
                rentadosDelta = fin.Rentados - inicio.Rentados;
                devueltosDelta = fin.Devueltos - inicio.Devueltos;
            }

            _ = rentadosDelta.Should().BeGreaterThan(0, "debio ocurrir al menos un alquiler fisico en ArrayPool");
            _ = devueltosDelta.Should().Be(rentadosDelta, "todos los buffers fisicos alquilados deben devolverse simetricamente");
        }

        [Fact]
        public void ReiniciarConservaVersionMonotonicaEnSlotsRecicladosImpideAba()
        {
            long tokenArena1 = ValueLINQArenaManager.Alquilar();
            int idArena1 = TokenHelper.ObtenerIdTokenArena(tokenArena1);
            long gen1 = TokenHelper.ObtenerGeneracionTokenArena(tokenArena1);

            try
            {
                TablaSesiones<int> tabla = new(idArena1, gen1);

                ref MetadatosSesion<int> sesion1 = ref tabla.ObtenerMetadatos(16);
                long token1 = sesion1.Token;
                long version1 = TokenHelper.ObtenerVersion(token1);
                int slotIndex1 = TokenHelper.ObtenerSlotIndex(token1);

                long versionPrevia = version1;
                long tokenPrevio = token1;

                for (int ciclo = 2; ciclo <= 20; ciclo++)
                {
                    long tokenArenaCiclo = ValueLINQArenaManager.Alquilar();
                    int idArenaCiclo = TokenHelper.ObtenerIdTokenArena(tokenArenaCiclo);
                    long genCiclo = TokenHelper.ObtenerGeneracionTokenArena(tokenArenaCiclo);

                    try
                    {
                        tabla.Reiniciar(idArenaCiclo, genCiclo);

                        _ = tabla.IsMetadatoValido(tokenPrevio).Should().BeFalse("el token de la encarnacion previa debe ser invalido");

                        ref MetadatosSesion<int> sesionNueva = ref tabla.ObtenerMetadatos(16);
                        long tokenNuevo = sesionNueva.Token;
                        long versionNueva = TokenHelper.ObtenerVersion(tokenNuevo);
                        int slotIndexNuevo = TokenHelper.ObtenerSlotIndex(tokenNuevo);

                        _ = slotIndexNuevo.Should().Be(slotIndex1, "debe reutilizarse la misma ranura del tope de la pila");
                        _ = versionNueva.Should().BeGreaterThan(versionPrevia, "la version del slot debe ser estrictamente creciente a traves de los reinicios");
                        _ = versionNueva.Should().Be(versionPrevia + 1, "cada reutilizacion de la ranura debe incrementar la version en exactamente 1");

                        versionPrevia = versionNueva;
                        tokenPrevio = tokenNuevo;
                    }
                    finally
                    {
                        ValueLINQArenaManager.Liberar(tokenArenaCiclo);
                    }
                }
            }
            finally
            {
                ValueLINQArenaManager.Liberar(tokenArena1);
            }
        }

        [Fact]
        public void CicloArenaEstacionarioExtendidoCienCiclosAsignaCeroBytesEnElMonton()
        {
            int[] datos = [1, 2, 3, 4, 5, 6, 7, 8];

            using (ValueLINQArena arenaCalentamiento = ValueLINQArena.Crear())
            {
                using ValueLINQStruct<int> q = datos.ToValueQuery(arenaCalentamiento);
                int sumaCalentamiento = 0;
                foreach (int v in q)
                    sumaCalentamiento += v;
                _ = sumaCalentamiento.Should().Be(36);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();

            int acumulado = 0;
            long asignadoAntes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 150; i++)
            {
                using ValueLINQArena arena = ValueLINQArena.Crear();
                using ValueLINQStruct<int> query = datos.ToValueQuery(arena);
                int suma = 0;
                foreach (int v in query)
                    suma += v;
                acumulado += suma;
            }
            long asignadoDespues = GC.GetAllocatedBytesForCurrentThread();

            _ = acumulado.Should().Be(36 * 150);
            _ = (asignadoDespues - asignadoAntes).Should().Be(0,
                "150 ciclos consecutivos de arena deben asignar estrictamente 0 bytes en el montón");
        }

        #endregion
    }
}

