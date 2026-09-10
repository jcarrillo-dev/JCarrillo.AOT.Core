using System;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Threading;

namespace JCarrillo.AOT.Core.Tests.Diagnostico
{
    /// <summary>
    /// Escuchador de diagnóstico derivado de <see cref="EventListener"/> para capturar
    /// y contabilizar de forma atómica los eventos físicos emitidos por <c>System.Buffers.ArrayPoolEventSource</c>.
    /// </summary>
    public sealed class ArrayPoolDiagnosticsListener : EventListener
    {
        private const string TargetEventSourceName = "System.Buffers.ArrayPoolEventSource";

        private readonly object _lock = new();
        private EventSource? _arrayPoolSource;
        private int _isDisposed;

        private long _bufferRentedCount;
        private long _bufferReturnedCount;
        private long _bufferAllocatedCount;

        /// <summary>
        /// Obtiene el número total de eventos de alquiler (<c>BufferRented</c>) registrados.
        /// </summary>
        public long BufferRentedCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => Interlocked.Read(ref _bufferRentedCount);
        }

        /// <summary>
        /// Obtiene el número total de eventos de devolución (<c>BufferReturned</c>) registrados.
        /// </summary>
        public long BufferReturnedCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => Interlocked.Read(ref _bufferReturnedCount);
        }

        /// <summary>
        /// Obtiene el número total de eventos de asignación física (<c>BufferAllocated</c>) registrados.
        /// </summary>
        public long BufferAllocatedCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => Interlocked.Read(ref _bufferAllocatedCount);
        }

        /// <summary>
        /// Obtiene la cantidad neta de búferes actualmente activos (alquilados pero aún no devueltos).
        /// </summary>
        public long ActiveBuffersCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferRentedCount - BufferReturnedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="BufferRentedCount"/>.
        /// </summary>
        public long Rentados
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferRentedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="BufferReturnedCount"/>.
        /// </summary>
        public long Devueltos
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferReturnedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="BufferAllocatedCount"/>.
        /// </summary>
        public long Asignados
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferAllocatedCount;
        }

        /// <summary>
        /// Se ejecuta al inicializarse o crearse una fuente de eventos en el proceso.
        /// Activa la escucha sobre <c>System.Buffers.ArrayPoolEventSource</c>.
        /// </summary>
        /// <param name="eventSource">Fuente de eventos notificada.</param>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);

            if (_isDisposed != 0)
                return;

            if (string.Equals(eventSource.Name, TargetEventSourceName, StringComparison.Ordinal))
            {
                lock (_lock)
                    _arrayPoolSource = eventSource;

                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        /// <summary>
        /// Procesa sincrónicamente los eventos emitidos por las fuentes suscritas.
        /// Actualiza los contadores mediante primitivas atómicas.
        /// </summary>
        /// <param name="eventData">Metadatos del evento emitido.</param>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (_isDisposed != 0)
                return;

            if (!string.Equals(eventData.EventSource?.Name, TargetEventSourceName, StringComparison.Ordinal))
                return;

            switch (eventData.EventId)
            {
                case 1:
                    Interlocked.Increment(ref _bufferRentedCount);
                    break;
                case 2:
                    Interlocked.Increment(ref _bufferAllocatedCount);
                    break;
                case 3:
                    Interlocked.Increment(ref _bufferReturnedCount);
                    break;
            }
        }

        /// <summary>
        /// Restablece atómicamente todos los contadores de eventos a cero.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _bufferRentedCount, 0);
            Interlocked.Exchange(ref _bufferReturnedCount, 0);
            Interlocked.Exchange(ref _bufferAllocatedCount, 0);
        }

        /// <summary>
        /// Captura una instantánea inmutable con los valores actuales de los contadores.
        /// </summary>
        /// <returns>Estructura inmutable con el estado actual.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstantaneaArrayPool CapturarInstantanea()
            => new(BufferRentedCount, BufferReturnedCount, BufferAllocatedCount);

        /// <summary>
        /// Sinónimo de <see cref="CapturarInstantanea"/>.
        /// </summary>
        /// <returns>Estructura inmutable con el estado actual.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstantaneaArrayPool ObtenerInstantanea()
            => CapturarInstantanea();

        /// <summary>
        /// Calcula la variación neta de eventos respecto a una instantánea previa.
        /// </summary>
        /// <param name="inicio">Instantánea de referencia inicial.</param>
        /// <returns>Estructura diferencial con la variación de contadores.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstantaneaArrayPool ObtenerDelta(in InstantaneaArrayPool inicio)
        {
            InstantaneaArrayPool actual = CapturarInstantanea();
            return inicio.CalcularDelta(in actual);
        }

        /// <summary>
        /// Crea un ámbito de diagnóstico asignado en pila para delimitar bloques mediante <see cref="IDisposable"/>.
        /// </summary>
        /// <returns>Estructura de ámbito contextual.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AmbitoDiagnosticoArrayPool CrearAmbito()
            => new(this);

        /// <summary>
        /// Desactiva la suscripción a eventos y libera los recursos del escuchador.
        /// </summary>
        public override void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
                return;

            lock (_lock)
                if (_arrayPoolSource is not null)
                {
                    try
                    {
                        DisableEvents(_arrayPoolSource);
                    }
                    catch
                    {
                        // Supresión defensiva al desasociar
                    }

                    _arrayPoolSource = null;
                }

            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Registro inmutable en pila (<see langword="struct"/>) que captura el estado
    /// de los contadores en un instante específico.
    /// </summary>
    public readonly record struct InstantaneaArrayPool(
        long BufferRentedCount,
        long BufferReturnedCount,
        long BufferAllocatedCount)
    {
        /// <summary>
        /// Alias en español para <see cref="BufferRentedCount"/>.
        /// </summary>
        public long Rentados
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferRentedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="BufferReturnedCount"/>.
        /// </summary>
        public long Devueltos
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferReturnedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="BufferAllocatedCount"/>.
        /// </summary>
        public long Asignados
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferAllocatedCount;
        }

        /// <summary>
        /// Obtiene el balance neto de búferes activos en esta instantánea.
        /// </summary>
        public long ActiveBuffersCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => BufferRentedCount - BufferReturnedCount;
        }

        /// <summary>
        /// Alias en español para <see cref="ActiveBuffersCount"/>.
        /// </summary>
        public long Activos
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => ActiveBuffersCount;
        }

        /// <summary>
        /// Calcula la diferencia neta de contadores entre la instantánea actual y una posterior.
        /// </summary>
        /// <param name="posterior">Instantánea posterior de comparación.</param>
        /// <returns>Diferencial de contadores resultante.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstantaneaArrayPool CalcularDelta(in InstantaneaArrayPool posterior)
            => new(
                posterior.BufferRentedCount - BufferRentedCount,
                posterior.BufferReturnedCount - BufferReturnedCount,
                posterior.BufferAllocatedCount - BufferAllocatedCount);

        /// <summary>
        /// Calcula la diferencia neta de contadores entre dos instantáneas dadas.
        /// </summary>
        /// <param name="inicio">Instantánea inicial de referencia.</param>
        /// <param name="fin">Instantánea final posterior.</param>
        /// <returns>Diferencial de contadores resultante.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InstantaneaArrayPool CalcularDelta(InstantaneaArrayPool inicio, InstantaneaArrayPool fin)
            => inicio.CalcularDelta(in fin);
    }

    /// <summary>
    /// Ámbito de diagnóstico asignado en pila para delimitar bloques de ejecución y evaluar deltas de búferes.
    /// </summary>
    public readonly struct AmbitoDiagnosticoArrayPool : IDisposable
    {
        private readonly ArrayPoolDiagnosticsListener _listener;
        private readonly InstantaneaArrayPool _inicio;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AmbitoDiagnosticoArrayPool(ArrayPoolDiagnosticsListener listener)
        {
            _listener = listener;
            _inicio = listener.CapturarInstantanea();
        }

        /// <summary>
        /// Obtiene la instantánea capturada al inicio del ámbito.
        /// </summary>
        public InstantaneaArrayPool Inicio
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
                => _inicio;
        }

        /// <summary>
        /// Obtiene el delta de contadores acumulado desde la creación de este ámbito.
        /// </summary>
        /// <returns>Diferencial de contadores de ArrayPool.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public InstantaneaArrayPool ObtenerDelta()
            => _listener.ObtenerDelta(in _inicio);

        /// <summary>
        /// Cierre del ámbito de diagnóstico.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
        }
    }
}
