using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using JCarrillo.AOT.Core.Benchmarks.Infraestructura;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

namespace JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Arena
{
    [Config(typeof(CoreBenchmarkConfig))]
    public class ArenaCicloVidaBenchmarks
    {
        private const int HilosContencion = 8;
        private const int OperacionesPorHilo = 10_000;
        private readonly Consumer _consumer = new();

        [Benchmark(Baseline = true)]
        public void AsignacionGCHeap()
        {
            List<int> list = new(64);
            _consumer.Consume(list);
        }

        [Benchmark]
        public void CrearYDisponerArenaTransitoria()
            => ValueLINQArena.Crear().Dispose();

        [Benchmark]
        public void CrearYDisponerArenaPersistente()
            => ValueLINQArena.Crear(persistente: true).Dispose();

        [Benchmark(OperationsPerInvoke = HilosContencion * OperacionesPorHilo)]
        public void CrearYDisponerArenaConcurrente()
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
    }
}
