using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace JCarrillo.AOT.Core.Extensiones.Boxing
{
    /// <summary>
    /// Proporciona métodos de extensión para verificar y validar en tiempo de ejecución
    /// que los structs de alto rendimiento no sufran boxing ni sean ubicados en el heap.
    /// </summary>
    public static partial class BoxingExtensions
    {
        [ThreadStatic]
        private static nuint _stackLow;

        [ThreadStatic]
        private static nuint _stackHigh;

        /// <summary>
        /// Valida que el struct especificado por referencia esté ubicado dentro de los límites de la pila (stack) del hilo actual.
        /// Si la dirección de memoria de la estructura está fuera de dichos límites, se determina que el struct ha sido boxeado
        /// en el heap, violando los principios de zero-allocation.
        /// </summary>
        /// <typeparam name="T">El tipo de la estructura que se va a validar. Debe ser un <see langword="struct"/>.</typeparam>
        /// <param name="value">La referencia al struct que se desea validar.</param>
        /// <exception cref="InvalidOperationException">
        /// Se lanza de forma inmediata si se detecta que el struct no reside en el stack (es decir, se ha realizado boxing o
        /// se encuentra alojado en el heap). La excepción es lanzada a través de un método auxiliar no-inlineable
        /// para mantener pequeña y optimizada la ruta de ejecución caliente.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ValidarNoBoxeado<T>(this ref T value) where T : struct
        {
            nuint thisPtr = (nuint)Unsafe.AsPointer(ref value);

            if (_stackLow == 0)
                InitializeStackLimits();

            if (thisPtr < _stackLow || thisPtr > _stackHigh)
            {
                InitializeStackLimits();
                if (thisPtr < _stackLow || thisPtr > _stackHigh)
                {
                    ThrowBoxingDetected(typeof(T).Name);
                }
            }
        }

        /// <summary>
        /// Valida específicamente que una instancia de <see cref="SemaphoreLock"/> por referencia de sólo lectura (<see langword="in"/>)
        /// esté ubicada dentro de los límites de la pila (stack) del hilo actual.
        /// </summary>
        /// <param name="value">La referencia de solo lectura a la estructura <see cref="SemaphoreLock"/>.</param>
        /// <exception cref="InvalidOperationException">
        /// Se lanza si se detecta que el struct <see cref="SemaphoreLock"/> ha sido boxeado o ubicado en el heap.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ValidarNoBoxeado(this in SemaphoreLock value)
        {
            nuint thisPtr = (nuint)Unsafe.AsPointer(ref Unsafe.AsRef(in value));

            if (_stackLow == 0)
                InitializeStackLimits();

            if (thisPtr < _stackLow || thisPtr > _stackHigh)
            {
                InitializeStackLimits();
                if (thisPtr < _stackLow || thisPtr > _stackHigh)
                {
                    ThrowBoxingDetected(nameof(SemaphoreLock));
                }
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
                return;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try
                {
                    byte* attr = stackalloc byte[64];
                    nint thread = pthread_self();
                    if (pthread_getattr_np(thread, attr) == 0)
                    {
                        void* stackaddr = null;
                        nuint stacksize = 0;
                        if (pthread_attr_getstack(attr, &stackaddr, &stacksize) == 0)
                        {
                            _stackLow = (nuint)stackaddr;
                            _stackHigh = _stackLow + stacksize;
                            _ = pthread_attr_destroy(attr);
                            return;
                        }
                        _ = pthread_attr_destroy(attr);
                    }
                }
                catch
                {
                    // Fallback to estimation
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    nint thread = pthread_self();
                    void* stackaddr = pthread_get_stackaddr_np(thread);
                    nuint stacksize = pthread_get_stacksize_np(thread);
                    if (stackaddr != null && stacksize > 0)
                    {
                        _stackHigh = (nuint)stackaddr;
                        _stackLow = _stackHigh - stacksize;
                        return;
                    }
                }
                catch
                {
                    // Fallback to estimation
                }
            }

            byte stackVar = 0;
            nuint currentStack = (nuint)Unsafe.AsPointer(ref stackVar);

            _stackLow = currentStack - (1024 * 1024);
            _stackHigh = currentStack + (16 * 1024 * 1024);
        }

        [LibraryImport("kernel32.dll")]
        private static unsafe partial void GetCurrentThreadStackLimits(nuint* lowLimit, nuint* highLimit);

        [LibraryImport("pthread", EntryPoint = "pthread_self")]
        private static partial nint pthread_self();

        [LibraryImport("pthread", EntryPoint = "pthread_getattr_np")]
        private static unsafe partial int pthread_getattr_np(nint thread, byte* attr);

        [LibraryImport("pthread", EntryPoint = "pthread_attr_getstack")]
        private static unsafe partial int pthread_attr_getstack(byte* attr, void** stackaddr, nuint* stacksize);

        [LibraryImport("pthread", EntryPoint = "pthread_attr_destroy")]
        private static unsafe partial int pthread_attr_destroy(byte* attr);

        [LibraryImport("pthread", EntryPoint = "pthread_get_stackaddr_np")]
        private static unsafe partial void* pthread_get_stackaddr_np(nint thread);

        [LibraryImport("pthread", EntryPoint = "pthread_get_stacksize_np")]
        private static partial nuint pthread_get_stacksize_np(nint thread);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowBoxingDetected(string typeName)
            => throw new InvalidOperationException($"Error: Se ha detectado boxing o ubicación en el Heap para el struct {typeName}. Su uso está estrictamente restringido a la pila (Stack).");
    }
}
