#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.Tests.E2E;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using Xunit;

#if NET9_0_OR_GREATER
using JCarrillo.AOT.Core.Extensiones.ValueLINQ.Delay;
using JCarrillo.AOT.Core.ValueLINQ.Delay;
#endif

namespace JCarrillo.AOT.Core.Tests.Pruebas.ValueLINQ
{
    public class PruebasStatelessWhere
    {
        static PruebasStatelessWhere()
        {
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQGC).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(ValueLINQStateManager<int>).TypeHandle);
        }

        public readonly struct EsParPredicate : IWhereDelegado<int>
        {
            public bool Ejecutar(int x) => x % 2 == 0;
        }

        public readonly struct MayorQueDosPredicate : IWhereDelegado<int>
        {
            public bool Ejecutar(int x) => x > 2;
        }

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_FiltraElementosCorrectamente()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where(new EsParPredicate());

            // Verificar
            _ = filtered.IsValido.Should().BeTrue();
            _ = filtered.ToArrayStandard().Should().Equal(2, 4, 6);
        }

        [Fact]
        public void ValueLINQRefStruct_Where_StatelessPredicate_FiltraElementosCorrectamente()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            using ValueLINQRefStruct<int> query = array.ToValueRefQuery();

            // Actuar
            using ValueLINQRefStruct<int> filtered = query.Where(new EsParPredicate());

            // Verificar
            _ = filtered.IsValido.Should().BeTrue();
            _ = filtered.ToArrayStandard().Should().Equal(2, 4, 6);
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ValueLINQDelay_Where_StatelessPredicate_FiltraElementosCorrectamente()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            var lazyPipeline = query.Delay().Where(new EsParPredicate());

            int count = 0;
            int[] resultado = new int[3];
            foreach (ref readonly int item in lazyPipeline)
            {
                resultado[count++] = item;
            }

            // Verificar
            _ = count.Should().Be(3);
            _ = resultado.Should().Equal(2, 4, 6);
        }

        [Fact]
        public void ValueLINQArrayDelay_Where_StatelessPredicate_FiltraElementosCorrectamente()
        {
            // Preparar
            int[] array = [10, 15, 20, 25, 30];

            // Actuar
            var lazyPipeline = array.ToValueDelayQuery().Where(new EsParPredicate());

            int count = 0;
            int[] resultado = new int[3];
            foreach (ref readonly int item in lazyPipeline)
                resultado[count++] = item;

            // Verificar
            _ = count.Should().Be(3);
            _ = resultado.Should().Equal(10, 20, 30);
        }

        [Fact]
        public void ValueLINQRefDelay_Where_StatelessPredicate_FiltraElementosCorrectamente()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            using ValueLINQRefStruct<int> query = array.ToValueRefQuery();

            // Actuar
            var lazyPipeline = query.Delay().Where(new EsParPredicate());

            int count = 0;
            int[] resultado = new int[3];
            foreach (ref readonly int item in lazyPipeline)
            {
                resultado[count++] = item;
            }

            // Verificar
            _ = count.Should().Be(3);
            _ = resultado.Should().Equal(2, 4, 6);
        }
#endif

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_ZeroAllocations()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            int[] buffer = new int[3];

            // Actuar & Verificar
            AllocationAssert.AssertZeroAllocations(() =>
            {
                using ValueLINQStruct<int> query = array.ToValueQuery();
                using ValueLINQStruct<int> filtered = query.Where<int, EsParPredicate>(new EsParPredicate());
                int idx = 0;
                foreach (ref int item in filtered)
                {
                    buffer[idx++] = item;
                }
            });
        }

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_ArrayVacio()
        {
            // Preparar
            int[] array = [];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where<int, EsParPredicate>(new EsParPredicate());

            // Verificar
            _ = filtered.ToArrayStandard().Should().BeEmpty();
        }

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_SinCoincidencias()
        {
            // Preparar
            int[] array = [1, 3, 5, 7];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where<int, EsParPredicate>(new EsParPredicate());

            // Verificar
            _ = filtered.ToArrayStandard().Should().BeEmpty();
        }

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_TodosCoinciden()
        {
            // Preparar
            int[] array = [2, 4, 6, 8];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> filtered = query.Where<int, EsParPredicate>(new EsParPredicate());

            // Verificar
            _ = filtered.ToArrayStandard().Should().Equal(2, 4, 6, 8);
        }

        [Fact]
        public void ValueLINQStruct_Where_StatelessPredicate_Encadenado()
        {
            // Preparar
            int[] array = [1, 2, 3, 4, 5, 6];
            using ValueLINQStruct<int> query = array.ToValueQuery();

            // Actuar
            using ValueLINQStruct<int> step1 = query.Where(new EsParPredicate());
            using ValueLINQStruct<int> step2 = step1.Where(new MayorQueDosPredicate());

            // Verificar
            _ = step2.ToArrayStandard().Should().Equal(4, 6);
        }
    }
}
