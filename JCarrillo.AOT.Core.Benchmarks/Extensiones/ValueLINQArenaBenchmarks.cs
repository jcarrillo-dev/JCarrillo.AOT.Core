using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using System.Runtime.CompilerServices;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Benchmarks.Extensiones
{
    [SimpleJob(RuntimeMoniker.Net80)]
    [SimpleJob(RuntimeMoniker.NativeAot80)]
    [SimpleJob(RuntimeMoniker.Net90)]
    [SimpleJob(RuntimeMoniker.NativeAot90)]
    [SimpleJob(RuntimeMoniker.Net10_0)]
    [SimpleJob(RuntimeMoniker.NativeAot10_0)]
    [MemoryDiagnoser]
    [Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
    public class ValueLINQArenaBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private int[] _array = null!;
        private ValueLINQArena _arenaReutilizada;

        private struct EvenFilter : IWhereDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly bool Ejecutar(int item, int otro) => (item & 1) == 0;
        }

        private struct MultiplyByTwoSelector : ISelectDelegado<int, int>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly int Ejecutar(int item) => item * 2;
        }

        [GlobalSetup]
        public void Setup()
        {
            _array = new int[Size];
            for (int i = 0; i < Size; i++)
                _array[i] = i;

            // Arena de larga vida, reutilizada por muchas consultas (la tabla se materializa una sola vez).
            _arenaReutilizada = ValueLINQArena.Crear(persistente: true);
        }

        // Baseline: cadena Where+Select materializada en la arena ambiente (arena 0, enrutado implícito).
        [Benchmark(Baseline = true)]
        public int WhereSelect_Ambiente()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery()
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

        // La misma cadena en una arena explícita: mide el sobrecoste de la propagación de arena
        // por los operadores y del enrutado por id de arena, más el alquiler/liberación de la arena.
        [Benchmark]
        public int WhereSelect_ArenaExplicita()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();

            using PooledArray<int> resultado = _array
                .ToValueQuery(arena)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

        // La misma cadena sobre una arena YA materializada y reutilizada: el coste amortizado real
        // en estado estacionario (la tabla del tipo ya existe; solo se paga el enrutado por arena).
        [Benchmark]
        public int WhereSelect_ArenaReutilizada()
        {
            using PooledArray<int> resultado = _array
                .ToValueQuery(_arenaReutilizada)
                .Where(0, new EvenFilter())
                .Select<int, MultiplyByTwoSelector, int>(new MultiplyByTwoSelector())
                .ToArray();

            return resultado.Tamaño;
        }

        /// <summary>
        /// La misma cadena en el motor perezoso sobre la arena ambiente. Where y Select no crean sesiones
        /// en este motor, así que la fila mide el pipeline diferido puro y sirve de referencia para su gemela con arena.
        /// </summary>
        [Benchmark]
        public int DelayWhereSelect_Ambiente()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            using PooledArray<int> resultado = _array
                .ToValueDelayQuery()
                .Where(0, ref filter)
                .Select<MultiplyByTwoSelector, int>(ref selector)
                .ToArray();

            return resultado.Tamaño;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        /// <summary>
        /// La cadena perezosa adscrita a la arena reutilizada: mide el coste de transportar y validar las
        /// opciones de arena por la cadena de operadores, que debería ser indistinguible de la fila ambiente.
        /// </summary>
        [Benchmark]
        public int DelayWhereSelect_ArenaReutilizada()
        {
#if NET9_0_OR_GREATER
            EvenFilter filter = new();
            MultiplyByTwoSelector selector = new();

            using PooledArray<int> resultado = _array
                .ToValueDelayQuery(_arenaReutilizada)
                .Where(0, ref filter)
                .Select<MultiplyByTwoSelector, int>(ref selector)
                .ToArray();

            return resultado.Tamaño;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        /// <summary>
        /// Chunk perezoso sobre la arena ambiente: el único operador diferido que reserva un buffer de sesión,
        /// con la validación de sesión que ejecuta cada MoveNext incluida en la medición.
        /// </summary>
        [Benchmark]
        public int DelayChunk_Ambiente()
        {
#if NET9_0_OR_GREATER
            int elementos = 0;

            foreach (ReadOnlySpan<int> fragmento in _array.ToValueDelayQuery().Chunk(64))
                elementos += fragmento.Length;

            return elementos;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }

        /// <summary>
        /// El mismo Chunk reservando su buffer en la arena reutilizada: mide el enrutado del alquiler a la tabla
        /// de la arena y la comprobación de arena viva al construir el operador, en estado estacionario.
        /// </summary>
        [Benchmark]
        public int DelayChunk_ArenaReutilizada()
        {
#if NET9_0_OR_GREATER
            int elementos = 0;

            foreach (ReadOnlySpan<int> fragmento in _array.ToValueDelayQuery(_arenaReutilizada).Chunk(64))
                elementos += fragmento.Length;

            return elementos;
#else
            throw new PlatformNotSupportedException("ValueLINQ Delay is only supported on .NET 9.0 or greater.");
#endif
        }
    }
}
