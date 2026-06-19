using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;

namespace JCarrillo.AOT.Core.Extensiones.Boxing
{
    public static partial class BoxingExtensions
    {
        [ThreadStatic]
        private static nuint _stackLow;

        [ThreadStatic]
        private static nuint _stackHigh;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ValidarNoBoxeado<T>(this ref T value) where T : struct
        {
            nuint thisPtr = (nuint)Unsafe.AsPointer(ref value);

            if (_stackLow == 0)
            {
                InitializeStackLimits();
            }

            if (thisPtr < _stackLow || thisPtr > _stackHigh)
            {
                ThrowBoxingDetected(typeof(T).Name);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ValidarNoBoxeado(this in SemaphoreLock value)
        {
            nuint thisPtr = (nuint)Unsafe.AsPointer(ref Unsafe.AsRef(in value));

            if (_stackLow == 0)
            {
                InitializeStackLimits();
            }

            if (thisPtr < _stackLow || thisPtr > _stackHigh)
            {
                ThrowBoxingDetected(nameof(SemaphoreLock));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static unsafe void InitializeStackLimits()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                nuint low, high;
                GetCurrentThreadStackLimits(&low, &high);
                _stackLow = low;
                _stackHigh = high;
            }
            else
            {
                byte stackVar = 0;
                nuint currentStack = (nuint)Unsafe.AsPointer(ref stackVar);

                _stackLow = currentStack - (1024 * 1024);
                _stackHigh = currentStack + (16 * 1024 * 1024);
            }
        }

        [LibraryImport("kernel32.dll")]
        private static unsafe partial void GetCurrentThreadStackLimits(nuint* lowLimit, nuint* highLimit);

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBoxingDetected(string typeName)
            => throw new InvalidOperationException($"Error: Se ha detectado boxing o ubicación en el Heap para el struct {typeName}. Su uso está estrictamente restringido a la pila (Stack).");
    }
}
