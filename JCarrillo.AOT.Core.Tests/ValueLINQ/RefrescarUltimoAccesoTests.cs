using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using System.Diagnostics;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class RefrescarUltimoAccesoTests
    {
        [Fact]
        public void DentroDelIntervaloNoActualizaElUltimoAcceso()
        {
            ref MetadatosSesion<int> metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(4);
            long token = metadatos.Token;
            try
            {
                long accesoInicial = metadatos.UltimoAcceso;

                ValueLINQStateManager<int>.RefrescarUltimoAcceso(token, TimeSpan.FromMinutes(10));

                metadatos.UltimoAcceso.Should().Be(accesoInicial);
            }
            finally
            {
                ValueLINQStateManager<int>.LiberarMetadatos(token);
            }
        }

        [Fact]
        public void SuperadoElIntervaloActualizaElUltimoAcceso()
        {
            ref MetadatosSesion<int> metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(4);
            long token = metadatos.Token;
            try
            {
                long accesoAntiguo = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 60;
                metadatos.UltimoAcceso = accesoAntiguo;

                ValueLINQStateManager<int>.RefrescarUltimoAcceso(token, TimeSpan.FromSeconds(30));

                metadatos.UltimoAcceso.Should().BeGreaterThan(accesoAntiguo);
            }
            finally
            {
                ValueLINQStateManager<int>.LiberarMetadatos(token);
            }
        }

        [Fact]
        public void ConTokenLiberadoNoReviveLaSesion()
        {
            ref MetadatosSesion<int> metadatos = ref ValueLINQStateManager<int>.ObtenerMetadatos(4);
            long token = metadatos.Token;
            ValueLINQStateManager<int>.LiberarMetadatos(token);

            ValueLINQStateManager<int>.RefrescarUltimoAcceso(token, TimeSpan.Zero);

            metadatos.UltimoAcceso.Should().Be(-1);
            metadatos.IsDisposed.Should().BeTrue();
        }
    }
}
