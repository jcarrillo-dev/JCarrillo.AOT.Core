using System;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("JCarrillo.AOT.Core.Tests")]
[assembly: InternalsVisibleTo("JCarrillo.AOT.Core.Benchmarks")]

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public static class TokenHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearToken(int slotIndex, long version) => (version << 12) | ((long)slotIndex & 0xFFFL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerSlotIndex(long token) => (int)(token & 0xFFFL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ObtenerVersion(long token) => (long)((ulong)token >> 12);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LeerToken(ref long ubicacion)
            => Environment.Is64BitProcess 
                ? Volatile.Read(ref ubicacion) 
                : Interlocked.Read(ref ubicacion);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EscribirToken(ref long ubicacion, long valor)
        {
            if (Environment.Is64BitProcess)
                Volatile.Write(ref ubicacion, valor);
            else
                Interlocked.Exchange(ref ubicacion, valor);
        }
    }
}
