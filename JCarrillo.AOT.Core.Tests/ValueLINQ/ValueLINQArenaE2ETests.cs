using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class ValueLINQArenaE2ETests
    {
        private struct MayorQue : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int umbral) => item > umbral;
        }

        private struct Doble : ISelectDelegado<int, int>
        {
            public readonly int Ejecutar(int item) => item * 2;
        }

        private struct ATexto : ISelectDelegado<int, string>
        {
            public readonly string Ejecutar(int item) => item.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static readonly int[] DatosArenaDispuesta = [1, 2, 3];
        private static readonly int[] DatosAmbiente = [10, 20];
        private static readonly int[] DatosConcatA = [1, 2];
        private static readonly int[] DatosConcatB = [3, 4];

        [Fact]
        public void CrearYDisponerUnaArenaEsCoherente()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            _ = arena.IsViva.Should().BeTrue();

            arena.Dispose();
            _ = arena.IsViva.Should().BeFalse();
        }

        [Fact]
        public void DisponerLaArenaLiberaLaConsultaCreadaEnElla()
        {
            int[] datos = [1, 2, 3, 4, 5];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<int> query = datos.ToValueQuery(arena);
            _ = query.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = query.IsValido.Should().BeFalse("al disponer la arena, su sesión debe liberarse");
        }

        [Fact]
        public void DisponerLaArenaLiberaElDestinoDeWhereAunqueSeOlvide()
        {
            int[] datos = [1, 2, 3, 4, 5, 6];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<int> filtrado = datos.ToValueQuery(arena).Where(3, new MayorQue());
            _ = filtrado.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = filtrado.IsValido.Should().BeFalse("el destino de Where debe vivir en la arena del origen y liberarse con ella");
        }

        [Fact]
        public void DisponerLaArenaLiberaElResultadoDeSelectAunqueCambieDeTipo()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arena = ValueLINQArena.Crear();

            ValueLINQStruct<string> proyectado = datos.ToValueQuery(arena).Select<int, ATexto, string>(new ATexto());
            _ = proyectado.IsValido.Should().BeTrue();

            arena.Dispose();

            _ = proyectado.IsValido.Should().BeFalse("la tabla del tipo string en esta arena también debe liberarse en bloque");
        }

        [Fact]
        public void UnaCadenaEnArenaProduceElResultadoCorrecto()
        {
            int[] datos = [1, 2, 3, 4, 5, 6];
            using ValueLINQArena arena = ValueLINQArena.Crear();

            List<int> resultado = [];
            foreach (int x in datos.ToValueQuery(arena).Where(3, new MayorQue()).Select<int, Doble, int>(new Doble()))
                resultado.Add(x);

            _ = resultado.Should().Equal(8, 10, 12);
        }

        [Fact]
        public void DisponerUnaArenaNoAfectaLaArenaAmbiente()
        {
            ValueLINQArena arena = ValueLINQArena.Crear();
            _ = DatosArenaDispuesta.ToValueQuery(arena);
            arena.Dispose();

            using PooledArray<int> resultado = DatosAmbiente.ToValueQuery().ToArray();

            _ = resultado.Tamaño.Should().Be(2);
            _ = resultado.Span[0].Should().Be(10);
            _ = resultado.Span[1].Should().Be(20);
        }

        [Fact]
        public void ConcatDentroDeLaMismaArenaFunciona()
        {
            using ValueLINQArena arena = ValueLINQArena.Crear();
            ValueLINQStruct<int> a = DatosConcatA.ToValueQuery(arena);
            ValueLINQStruct<int> b = DatosConcatB.ToValueQuery(arena);

            List<int> resultado = [];
            foreach (int x in a.Concat(b))
                resultado.Add(x);

            _ = resultado.Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void ConcatEntreArenasDistintasLanzaArenaCruzada()
        {
            int[] datos = [1, 2, 3];
            using ValueLINQArena arenaA = ValueLINQArena.Crear();
            using ValueLINQArena arenaB = ValueLINQArena.Crear();

            ValueLINQStruct<int> enA = datos.ToValueQuery(arenaA);
            ValueLINQStruct<int> enB = datos.ToValueQuery(arenaB);

            Action concatCruzado = () => enA.Concat(enB);

            _ = concatCruzado.Should().Throw<ValueLinqArenaCruzadaException>();
        }

        [Fact]
        public void ArenasDistintasAislanSusSesiones()
        {
            int[] datos = [1, 2, 3];
            ValueLINQArena arenaA = ValueLINQArena.Crear();
            ValueLINQArena arenaB = ValueLINQArena.Crear();

            ValueLINQStruct<int> enA = datos.ToValueQuery(arenaA);
            ValueLINQStruct<int> enB = datos.ToValueQuery(arenaB);

            arenaA.Dispose();

            _ = enA.IsValido.Should().BeFalse("disponer A libera solo lo de A");
            _ = enB.IsValido.Should().BeTrue("lo de B sobrevive");

            arenaB.Dispose();
            _ = enB.IsValido.Should().BeFalse();
        }
    }
}
