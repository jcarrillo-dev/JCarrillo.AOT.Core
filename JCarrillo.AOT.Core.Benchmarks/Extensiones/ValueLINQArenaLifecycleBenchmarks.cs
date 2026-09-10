using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.ValueLINQ.Arena;

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.NativeAot80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [SimpleJob(RuntimeMoniker.NativeAot90)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    [MemoryDiagnoser]
    public class ValueLINQArenaLifecycleBenchmarks
    {
        private const int HilosContencion = 8;
        private const int OperacionesPorHilo = 10_000;

        /// <summary>
        /// Coste del par alquilar/liberar una arena (slotmap FIFO + token de arena) en un solo hilo, sin contención.
        /// </summary>
        [Benchmark(Baseline = true)]
        public void CrearYDisponerArena()
            => ValueLINQArena.Crear().Dispose();

        /// <summary>
        /// El mismo par bajo ocho hilos simultáneos disputando el spinlock global del manager.
        /// </summary>
        /// <remarks>
        /// La columna Ratio expresa el multiplicador de la contención frente al coste aislado del baseline.
        /// El alta y el join de los hilos quedan dentro de la medición a propósito, amortizados entre las
        /// 80.000 operaciones de cada invocación (menos del 1% del total), a cambio de no medir nunca un
        /// pool de hilos a medio calentar.
        /// </remarks>
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
