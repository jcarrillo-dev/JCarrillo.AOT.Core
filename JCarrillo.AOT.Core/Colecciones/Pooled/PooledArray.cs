using JCarrillo.AOT.Core.Extensiones.Boxing;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Colecciones.Pooled
{
    /// <summary>
    /// Representa un arreglo inmutable de tipo <see langword="struct"/> que encapsula un búfer en memoria
    /// alquilado a partir de un <see cref="System.Buffers.ArrayPool{T}"/>.
    /// Esta estructura está diseñada para escenarios de ultra alto rendimiento y baja latencia, minimizando la presión sobre el recolector de basura (GC).
    /// </summary>
    /// <typeparam name="TItem">El tipo de los elementos almacenados en el arreglo.</typeparam>
    public record struct PooledArray<TItem> : IPooledStruct<TItem>
    {
        #region Constructor

        /// <summary>
        /// Inicializa una nueva instancia de la estructura <see cref="PooledArray{TItem}"/> alquilando un búfer
        /// con la capacidad inicial especificada desde el <see cref="System.Buffers.ArrayPool{TItem}.Shared"/> común.
        /// </summary>
        /// <param name="capacidadInicial">La capacidad inicial (número de elementos) requerida para el arreglo.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Se lanza cuando <paramref name="capacidadInicial"/> es menor o igual a cero.
        /// Esta validación asegura la correcta interacción con el pool subyacente y se implementa mediante un método helper estático
        /// para prevenir la contaminación de inlining en el JIT compilador.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledArray(int capacidadInicial)
        {
            if (capacidadInicial <= 0) ThrowArgumentOutOfRange(capacidadInicial);
            _items = ArrayPool<TItem>.Shared.Rent(capacidadInicial);
            _tamaño = capacidadInicial;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PooledArray(TItem[] items, int tamaño)
        {
            if (tamaño < 0 || tamaño > items.Length) ThrowArgumentOutOfRange(tamaño);
            _items = items;
            _tamaño = tamaño;
        }

        #endregion

        #region Tamaño

        private int _tamaño;

        /// <summary>
        /// Obtiene el número actual de elementos válidos en el arreglo.
        /// </summary>
        /// <value>La capacidad fija solicitada en la inicialización.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si se intenta acceder a la propiedad después de que los recursos subyacentes hayan sido liberados mediante <see cref="Dispose"/> o <see cref="DisposeAsync"/>.
        /// </exception>
        public readonly int Tamaño
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                return _tamaño;
            }
        }

        #endregion

        #region EsAmpliable

        private readonly bool _esAmpliable = false;

        /// <summary>
        /// Obtiene un valor que indica si la estructura puede crecer dinámicamente.
        /// En <see cref="PooledArray{TItem}"/>, esta propiedad siempre es <see langword="false"/>.
        /// </summary>
        public readonly bool EsAmpliable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _esAmpliable; }
        }

        #endregion

        #region Datos

        private TItem[]? _items;
        private Memory<TItem>? _memory;

        /// <summary>
        /// Obtiene una vista de acceso directo en memoria en forma de <see cref="Span{TItem}"/> sobre el arreglo alquilado.
        /// Proporciona acceso ultra rápido y seguro en el stack sin generar asignaciones de memoria adicionales en el heap.
        /// </summary>
        /// <value>Una estructura <see cref="Span{TItem}"/> que representa la ventana de memoria activa.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si el arreglo subyacente ya ha sido retornado al pool.
        /// </exception>
        public Span<TItem> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Memory.Span;
        }

        /// <summary>
        /// Obtiene una representación en memoria en forma de <see cref="Memory{TItem}"/> sobre el arreglo alquilado.
        /// Diseñado para operaciones asíncronas de alto rendimiento donde el ciclo de vida del búfer trasciende el stack frame del método actual.
        /// </summary>
        /// <value>Una estructura <see cref="Memory{TItem}"/> que apunta al búfer alquilado.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si el arreglo subyacente ya ha sido retornado al pool.
        /// </exception>
        public Memory<TItem> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed || _items is null) ThrowObjectDisposed();
                return _memory ??= new Memory<TItem>(_items, 0, _tamaño);
            }
        }

        #endregion

        #region Indice

        /// <summary>
        /// Obtiene una referencia directa al elemento ubicado en el índice de base cero especificado.
        /// </summary>
        /// <param name="indice">El índice del elemento a recuperar.</param>
        /// <value>La referencia directa (<see langword="ref"/>) al elemento en la posición dada, evitando copias innecesarias en el stack.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si el arreglo subyacente ya ha sido liberado y retornado al pool.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// Se lanza si el <paramref name="indice"/> está fuera de los límites definidos por el tamaño de la estructura.
        /// Utilizar la excepción nativa en lugar de validaciones complejas permite al compilador JIT optimizar el código
        /// eliminando comprobaciones redundantes de límites (array bounds check elimination).
        /// </exception>
        public readonly ref TItem this[int indice]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                TItem[]? items = _items;
                if (items is null || (uint)indice >= (uint)_tamaño) ThrowIndexOutOfRange(indice);
                return ref items[indice];
            }
        }

        #endregion

        #region Enumerable

        /// <summary>
        /// Retorna un enumerador de tipo struct sobre el búfer interno para permitir la iteración de la colección.
        /// Permite utilizar la sintaxis foreach mediante duck-typing sin incurrir en asignaciones de objetos ni boxing en el heap.
        /// </summary>
        /// <returns>Un enumerador de tipo struct de alto rendimiento (<see cref="Span{TItem}.Enumerator"/>) sobre los elementos activos.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<TItem>.Enumerator GetEnumerator()
            => Span.GetEnumerator();

        #endregion

        #region Disposed

        private bool _disposed = false;

        /// <summary>
        /// Obtiene un valor que indica si los recursos y el búfer subyacente ya han sido devueltos al pool.
        /// </summary>
        public readonly bool EstaDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _disposed;
        }

        /// <summary>
        /// Libera los recursos de la estructura de forma síncrona y devuelve el búfer de memoria alquilado
        /// al <see cref="System.Buffers.ArrayPool{TItem}.Shared"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Se lanza si se detecta que la estructura ha sido copiada o boxeada en el heap, violando la regla
        /// de confinamiento en el stack (stack containment). Esta validación la realiza el método <see cref="BoxingExtensions.ValidarNoBoxeado{T}"/>.
        /// </exception>
        public void Dispose()
        {
            this.ValidarNoBoxeado();
            if (_disposed) return;
            DisposePrivate();
        }

        /// <summary>
        /// Libera los recursos de la estructura de forma asíncrona y devuelve el búfer de memoria alquilado
        /// al <see cref="System.Buffers.ArrayPool{TItem}.Shared"/>.
        /// </summary>
        /// <returns>Una <see cref="ValueTask"/> optimizada que representa la tarea de liberación completada de forma inmediata.</returns>
        /// <remarks>
        /// A diferencia de <see cref="Dispose()"/>, la llamada a <see cref="DisposeAsync()"/> NO incluye la validación de no-boxing
        /// mediante <see cref="BoxingExtensions.ValidarNoBoxeado{T}"/>. Esto se debe a que la máquina de estados generada
        /// por el compilador para métodos asíncronos o el contexto de ejecución asíncrona pueden trasladar la estructura
        /// al heap de forma legítima, lo que causaría falsos positivos en la detección de boxing.
        /// Además, omitir esta validación en la ruta asíncrona optimiza el rendimiento y la compatibilidad con compilación Native AOT.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            DisposePrivate();
            return ValueTask.CompletedTask;
        }

        private void DisposePrivate()
        {
            _disposed = true;
            if (_items != null)
            {
                _tamaño = 0;
                ArrayPool<TItem>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
                _items = null;
            }
        }

        #endregion

        #region IntentarAmpliar

        /// <summary>
        /// Intenta cambiar el tamaño de la estructura interna.
        /// En <see cref="PooledArray{TItem}"/>, dado que el tamaño es fijo y no ampliable, este método siempre retorna <see langword="false"/>.
        /// </summary>
        /// <param name="nuevoTamaño">El nuevo tamaño sugerido.</param>
        /// <returns>Siempre retorna <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool IntentarAmpliar(int nuevoTamaño)
            => false;

        #endregion

        #region Helpers de Excepciones (Evitan contaminación del JIT)

        private const string NombreClase = "PooledArray<" + nameof(TItem) + ">";

        [DoesNotReturn]
        private static void ThrowObjectDisposed()
            => throw new ObjectDisposedException(NombreClase);

        [DoesNotReturn]
        private static void ThrowIndexOutOfRange(int indice)
            => throw new IndexOutOfRangeException($"El índice {indice} está fuera del rango válido.");

        [DoesNotReturn]
        private static void ThrowArgumentOutOfRange(int capacidad)
            => throw new ArgumentOutOfRangeException(nameof(capacidad), $"La capacidad inicial ({capacidad}) debe ser mayor que cero.");

        #endregion

        #region Clear

        /// <summary>
        /// Limpia el contenido del búfer interno restableciendo los elementos a sus valores predeterminados (default).
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si el arreglo subyacente ya ha sido liberado y retornado al pool.
        /// </exception>
        public void Clear()
        {
            if (_disposed || _items is null) ThrowObjectDisposed();
            Span.Clear();
        }

        #endregion

    }
}
