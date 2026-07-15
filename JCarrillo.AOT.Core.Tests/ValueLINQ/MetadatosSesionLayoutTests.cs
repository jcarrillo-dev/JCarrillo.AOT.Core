using FluentAssertions;
using JCarrillo.AOT.Core.ValueLINQ;
using System.Runtime.CompilerServices;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.ValueLINQ
{
    public class MetadatosSesionLayoutTests
    {
        [Fact]
        public void MetadatosSesionOcupaUnaLineaDeCacheEnProcesos64Bit()
        {
            if (!Environment.Is64BitProcess)
                return;

            Unsafe.SizeOf<MetadatosSesion<int>>().Should().Be(64);
            Unsafe.SizeOf<MetadatosSesion<object>>().Should().Be(64);
        }

        [Fact]
        public void SpinLockSlotOcupaUnaLineaDeCache()
            => Unsafe.SizeOf<SpinLockSlot>().Should().Be(64);

        [Fact]
        public void LaGeometriaDeLaConfiguracionEsCoherente()
        {
            _ = (ValueLINQConfig.Particiones * ValueLINQConfig.SlotsEnParticion).Should().Be(ValueLINQConfig.Slots);
            _ = ValueLINQConfig.SlotsParticionBits.Should().BeInRange(1, ValueLINQConfig.SlotBits);
            _ = ValueLINQConfig.ParticionBits.Should().BeGreaterThanOrEqualTo(0);
            _ = (ValueLINQConfig.SlotBits + ValueLINQConfig.ArenaBits + ValueLINQConfig.ArenaGenBits + ValueLINQConfig.VersionBits)
                    .Should().Be(64, "los cuatro campos del token de sesión deben ocupar exactamente 64 bits");
            _ = ValueLINQConfig.VersionBits.Should().BeGreaterThan(0);
            _ = ValueLINQConfig.ArenaGenMask.Should().Be((1L << ValueLINQConfig.ArenaGenBits) - 1);
            _ = ValueLINQConfig.SlotMask.Should().Be(ValueLINQConfig.Slots - 1);
            _ = ValueLINQConfig.SlotsParticionMask.Should().Be(ValueLINQConfig.SlotsEnParticion - 1);
            _ = ValueLINQConfig.TamañoTabla.Should().Be(ValueLINQConfig.Slots);
        }
    }
}
