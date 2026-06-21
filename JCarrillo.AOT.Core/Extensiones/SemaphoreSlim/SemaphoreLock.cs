using JCarrillo.AOT.Core.Extensiones.Boxing;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.SemaphoreSlim
{
    /// <summary>
    /// Representa un bloqueo de tipo <see langword="struct"/> inmutable que libera automáticamente
    /// el recurso <see cref="System.Threading.SemaphoreSlim"/> asociado al ser desechado.
    /// Diseñado para optimizar el rendimiento mediante el patrón de cero asignaciones en el heap.
    /// </summary>
    public readonly record struct SemaphoreLock : IDisposable
    {
        private readonly System.Threading.SemaphoreSlim _semaphore;

        /// <summary>
        /// Inicializa una nueva instancia de la estructura <see cref="SemaphoreLock"/> vinculada al semáforo especificado.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente que se desea bloquear y liberar.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SemaphoreLock(System.Threading.SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        /// <summary>
        /// Libera el bloqueo de forma síncrona, incrementando en uno el contador de hilos permitidos en el semáforo subyacente.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Se lanza si se detecta que la estructura ha sido copiada o boxeada en el heap, violando su confinamiento en el stack.
        /// Esta validación es realizada por <see cref="BoxingExtensions.ValidarNoBoxeado(in SemaphoreLock)"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            this.ValidarNoBoxeado();
            _semaphore.Release();
        }

        /// <summary>
        /// Libera el bloqueo de forma asíncrona, incrementando en uno el contador de hilos permitidos en el semáforo subyacente.
        /// </summary>
        /// <returns>Una <see cref="ValueTask"/> optimizada que representa la tarea de liberación completada inmediatamente.</returns>
        /// <remarks>
        /// A diferencia de <see cref="Dispose()"/>, la llamada a <see cref="DisposeAsync()"/> NO incluye la validación de no-boxing
        /// mediante <see cref="BoxingExtensions.ValidarNoBoxeado(in SemaphoreLock)"/>. Esto se debe a que la máquina de estados generada
        /// por el compilador para métodos asíncronos o el contexto de ejecución asíncrona pueden trasladar la estructura
        /// al heap de forma legítima, lo que causaría falsos positivos en la detección de boxing.
        /// Además, omitir esta validación en la ruta asíncrona optimiza el rendimiento y la compatibilidad con compilación Native AOT.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
