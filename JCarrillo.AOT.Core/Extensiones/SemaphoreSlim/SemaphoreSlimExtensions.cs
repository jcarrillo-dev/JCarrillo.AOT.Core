using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.SemaphoreSlim
{
    /// <summary>
    /// Proporciona métodos de extensión de alto rendimiento para <see cref="System.Threading.SemaphoreSlim"/>
    /// que permiten adquirir bloqueos reutilizando structs inmutables para mitigar las asignaciones en el heap.
    /// </summary>
    public static class SemaphoreSlimExtensions
    {
        #region Esperar (Wait)

        /// <summary>
        /// Bloquea síncronamente el hilo actual hasta que pueda entrar en el semáforo, y retorna un <see cref="SemaphoreLock"/>
        /// para su posterior liberación segura en un bloque using.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <returns>Un <see cref="SemaphoreLock"/> que administra la liberación del semáforo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore)
            => Esperar(semaphore, CancellationToken.None);

        /// <summary>
        /// Bloquea síncronamente el hilo actual hasta que pueda entrar en el semáforo o expire el tiempo de espera,
        /// y retorna un <see cref="SemaphoreLock"/> para su posterior liberación segura en un bloque using.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="millisecondsTimeout">El número de milisegundos de espera, o -1 para una espera infinita.</param>
        /// <returns>Un <see cref="SemaphoreLock"/> que administra la liberación del semáforo.</returns>
        /// <exception cref="TimeoutException">
        /// Se lanza si expira el tiempo establecido en <paramref name="millisecondsTimeout"/> antes de adquirir el bloqueo.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout)
            => Esperar(semaphore, millisecondsTimeout, CancellationToken.None);

        /// <summary>
        /// Bloquea síncronamente el hilo actual observando el token de cancelación, y retorna un <see cref="SemaphoreLock"/>
        /// para su posterior liberación segura en un bloque using.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="cancellationToken">El token de cancelación a observar.</param>
        /// <returns>Un <see cref="SemaphoreLock"/> que administra la liberación del semáforo.</returns>
        /// <exception cref="OperationCanceledException">
        /// Se lanza cuando se solicita la cancelación a través de <paramref name="cancellationToken"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            semaphore.Wait(cancellationToken);
            return new SemaphoreLock(semaphore);
        }

        /// <summary>
        /// Bloquea síncronamente el hilo actual observando el tiempo de espera y el token de cancelación,
        /// y retorna un <see cref="SemaphoreLock"/> para su posterior liberación segura en un bloque using.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="millisecondsTimeout">El número de milisegundos de espera, o -1 para una espera infinita.</param>
        /// <param name="cancellationToken">El token de cancelación a observar.</param>
        /// <returns>Un <see cref="SemaphoreLock"/> que administra la liberación del semáforo.</returns>
        /// <exception cref="TimeoutException">
        /// Se lanza si expira el tiempo establecido en <paramref name="millisecondsTimeout"/> antes de adquirir el bloqueo.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Se lanza cuando se solicita la cancelación a través de <paramref name="cancellationToken"/>.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SemaphoreLock Esperar(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!semaphore.Wait(millisecondsTimeout, cancellationToken))
                ThrowTimeout(millisecondsTimeout);
            return new SemaphoreLock(semaphore);
        }

        #endregion

        #region EsperarAsync (WaitAsync)

        /// <summary>
        /// Bloquea asíncronamente el acceso al semáforo y retorna un <see cref="ValueTask{SemaphoreLock}"/>
        /// para su posterior liberación asíncrona segura.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <returns>Un <see cref="ValueTask{SemaphoreLock}"/> que se completa con la adquisición del bloqueo.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore)
            => EsperarAsync(semaphore, CancellationToken.None);

        /// <summary>
        /// Bloquea asíncronamente el acceso al semáforo especificando un tiempo de espera, y retorna un <see cref="ValueTask{SemaphoreLock}"/>
        /// para su posterior liberación asíncrona segura.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="millisecondsTimeout">El número de milisegundos de espera, o -1 para una espera infinita.</param>
        /// <returns>Un <see cref="ValueTask{SemaphoreLock}"/> que se completa con la adquisición del bloqueo.</returns>
        /// <exception cref="TimeoutException">
        /// Se lanza si expira el tiempo establecido en <paramref name="millisecondsTimeout"/> antes de adquirir el bloqueo.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout)
            => EsperarAsync(semaphore, millisecondsTimeout, CancellationToken.None);

        /// <summary>
        /// Bloquea asíncronamente el acceso al semáforo observando un token de cancelación, y retorna un <see cref="ValueTask{SemaphoreLock}"/>.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="cancellationToken">El token de cancelación a observar.</param>
        /// <returns>Un <see cref="ValueTask{SemaphoreLock}"/> que se completa con la adquisición del bloqueo.</returns>
        /// <exception cref="OperationCanceledException">
        /// Se lanza cuando se solicita la cancelación a través de <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// Para maximizar el rendimiento, el método implementa una ruta rápida (fast-path): si el semáforo se puede adquirir de manera inmediata
        /// sin bloqueo (mediante <c>Wait(0)</c>), se retorna un <see cref="ValueTask{SemaphoreLock}"/> con el valor ya resuelto,
        /// evitando por completo la instanciación de la máquina de estados asíncrona del compilador.
        /// Si el semáforo está ocupado, la ejecución continúa por la ruta lenta asíncrona optimizada con pooling.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            if (semaphore.Wait(0, cancellationToken))
            {
                return new ValueTask<SemaphoreLock>(new SemaphoreLock(semaphore));
            }
            return EsperarAsyncSlow(semaphore, cancellationToken);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        private static async ValueTask<SemaphoreLock> EsperarAsyncSlow(System.Threading.SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new SemaphoreLock(semaphore);
        }

        /// <summary>
        /// Bloquea asíncronamente el acceso al semáforo observando un tiempo de espera y un token de cancelación, y retorna un <see cref="ValueTask{SemaphoreLock}"/>.
        /// </summary>
        /// <param name="semaphore">El semáforo subyacente.</param>
        /// <param name="millisecondsTimeout">El número de milisegundos de espera, o -1 para una espera infinita.</param>
        /// <param name="cancellationToken">El token de cancelación a observar.</param>
        /// <returns>Un <see cref="ValueTask{SemaphoreLock}"/> que se completa con la adquisición del bloqueo.</returns>
        /// <exception cref="TimeoutException">
        /// Se lanza si expira el tiempo establecido en <paramref name="millisecondsTimeout"/> antes de adquirir el bloqueo.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Se lanza cuando se solicita la cancelación a través de <paramref name="cancellationToken"/>.
        /// </exception>
        /// <remarks>
        /// Para maximizar el rendimiento, el método implementa una ruta rápida (fast-path): si el semáforo se puede adquirir de manera inmediata
        /// sin bloqueo (mediante <c>Wait(0)</c>), se retorna un <see cref="ValueTask{SemaphoreLock}"/> con el valor ya resuelto,
        /// evitando por completo la instanciación de la máquina de estados asíncrona del compilador.
        /// Si el semáforo está ocupado, la ejecución continúa por la ruta lenta asíncrona optimizada con pooling.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<SemaphoreLock> EsperarAsync(this System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (semaphore.Wait(0, cancellationToken))
            {
                return new ValueTask<SemaphoreLock>(new SemaphoreLock(semaphore));
            }
            return EsperarAsyncSlow(semaphore, millisecondsTimeout, cancellationToken);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        private static async ValueTask<SemaphoreLock> EsperarAsyncSlow(System.Threading.SemaphoreSlim semaphore, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            if (!await semaphore.WaitAsync(millisecondsTimeout, cancellationToken).ConfigureAwait(false))
                ThrowTimeout(millisecondsTimeout);
            return new SemaphoreLock(semaphore);
        }

        #endregion

        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        private static void ThrowTimeout(int timeout)
            => throw new TimeoutException($"No se pudo obtener el semáforo en {timeout} milisegundos.");
    }
}
