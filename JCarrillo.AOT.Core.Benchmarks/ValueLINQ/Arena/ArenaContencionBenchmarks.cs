using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Arena
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArenaContencionBenchmarks
    {
        private const int HilosContencion = 8;
        private const int OperacionesPorHilo = 10_000;
        private static readonly Consumer _consumer = new();

        [Benchmark(Baseline = true, OperationsPerInvoke = HilosContencion * OperacionesPorHilo)]
        public void AsignacionGCHeapConcurrente()
        {
            Thread[] hilos = new Thread[HilosContencion];

            for (int i = 0; i < hilos.Length; i++)
            {
                hilos[i] = new Thread(static () =>
                {
                    for (int operacion = 0; operacion < OperacionesPorHilo; operacion++)
                    {
                        List<int> list = new(64);
                        _consumer.Consume(list);
                    }
                });
                hilos[i].Start();
            }

            foreach (Thread hilo in hilos)
                hilo.Join();
        }

        [Benchmark(OperationsPerInvoke = HilosContencion * OperacionesPorHilo)]
        public void CrearYDisponerArenaTransitoriaConcurrente()
        {
            Thread[] hilos = new Thread[HilosContencion];

            for (int i = 0; i < hilos.Length; i++)
            {
                hilos[i] = new Thread(static () =>
                {
                    for (int operacion = 0; operacion < OperacionesPorHilo; operacion++)
                        ValueLINQArena.Crear().Dispose();
                });
                hilos[i].Start();
            }

            foreach (Thread hilo in hilos)
                hilo.Join();
        }

        [Benchmark(OperationsPerInvoke = HilosContencion * OperacionesPorHilo)]
        public void CrearYDisponerArenaPersistenteConcurrente()
        {
            Thread[] hilos = new Thread[HilosContencion];

            for (int i = 0; i < hilos.Length; i++)
            {
                hilos[i] = new Thread(static () =>
                {
                    for (int operacion = 0; operacion < OperacionesPorHilo; operacion++)
                        ValueLINQArena.Crear(persistente: true).Dispose();
                });
                hilos[i].Start();
            }

            foreach (Thread hilo in hilos)
                hilo.Join();
        }
    }
}
