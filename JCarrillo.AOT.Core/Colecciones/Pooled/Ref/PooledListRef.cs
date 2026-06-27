using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Colecciones.Pooled.Ref
{
    /// <summary>
    /// Representa una lista mutable de crecimiento dinámico de tipo <see langword="ref struct"/> confinada estrictamente al stack.
    /// Encapsula un búfer en memoria alquilado a partir de un <see cref="System.Buffers.ArrayPool{T}"/>.
    /// Al ser un <see langword="ref struct"/>, se garantiza que nunca se ubicará en el heap, evitando por completo
    /// el boxing y reduciendo a cero la asignación de memoria en el recolector de basura (GC).
    /// </summary>
    /// <typeparam name="TItem">El tipo de los elementos contenidos en la lista.</typeparam>
    public ref struct PooledListRef<TItem>
    {
        #region Constructor

        /// <summary>
        /// Inicializa una nueva instancia de la estructura <see cref="PooledListRef{TItem}"/> con una capacidad predeterminada de 64 elementos.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledListRef() : this(64) { }

        /// <summary>
        /// Inicializa una nueva instancia de la estructura <see cref="PooledListRef{TItem}"/> con la capacidad inicial especificada.
        /// </summary>
        /// <param name="capacidadInicial">La capacidad inicial (número de elementos) requerida para la lista.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Se lanza cuando <paramref name="capacidadInicial"/> es menor o igual a cero.
        /// La validación se delega a un método estático auxiliar para evitar la contaminación de inlining en el compilador JIT.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledListRef(int capacidadInicial)
        {
            if (capacidadInicial <= 0) ThrowArgumentOutOfRange(capacidadInicial);

            _items = ArrayPool<TItem>.Shared.Rent(capacidadInicial);
            _indiceInserccion = 0;
        }

        /// <summary>
        /// Constructor interno para crear un PooledList a partir de un array existente y un tamaño específico.
        /// Solo usar en extensiones de ArrayPool para evitar copias innecesarias.
        /// </summary>
        /// <param name="items">Array devuelto por ArrayPool</param>
        /// <param name="tamaño">Tamaño del array solicitado por el desarrollador</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PooledListRef(TItem[] items, int tamaño)
        {
            if (tamaño < 0 || tamaño > items.Length) ThrowArgumentOutOfRange(tamaño);

            _items = items;
            _indiceInserccion = tamaño;
            _disposed = false;
        }

        #endregion

        #region Tamaño

        private int _indiceInserccion;

        /// <summary>
        /// Obtiene el número actual de elementos válidos en la lista.
        /// </summary>
        /// <value>La cantidad de elementos insertados activamente.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si se accede a la propiedad después de que los recursos subyacentes hayan sido devueltos al pool.
        /// </exception>
        public readonly int Tamaño
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                return _indiceInserccion;
            }
        }

        #endregion

        #region EsAmpliable

        private readonly bool _esAmpliable = true;

        /// <summary>
        /// Obtiene un valor que indica si la estructura puede crecer dinámicamente.
        /// En <see cref="PooledListRef{TItem}"/>, esta propiedad siempre es <see langword="true"/>.
        /// </summary>
        public readonly bool EsAmpliable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _esAmpliable; }
        }

        #endregion

        #region Datos

        private TItem[]? _items;

        /// <summary>
        /// Obtiene una vista de acceso directo en memoria en forma de <see cref="Span{TItem}"/> sobre los elementos activos de la lista.
        /// Proporciona acceso seguro y veloz en el stack sin provocar asignaciones en el heap.
        /// </summary>
        /// <value>Una ventana de tipo <see cref="Span{TItem}"/> sobre el búfer activo.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si el búfer interno ya ha sido retornado al pool.
        /// </exception>
        public readonly Span<TItem> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                return _items!.AsSpan(0, _indiceInserccion);
            }
        }

        #endregion

        #region Indice

        /// <summary>
        /// Obtiene una referencia directa al elemento ubicado en el índice de base cero especificado.
        /// </summary>
        /// <param name="indice">El índice de base cero del elemento a recuperar.</param>
        /// <value>La referencia directa (<see langword="ref"/>) al elemento en la posición dada.</value>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si la lista ha sido dispuesta previamente.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// Se lanza si el <paramref name="indice"/> está fuera de los límites de la lista.
        /// Se opta por la excepción nativa para permitir al compilador JIT omitir las comprobaciones redundantes de límites de array.
        /// </exception>
        public readonly ref TItem this[int indice]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                TItem[]? items = _items;
                if (items is null || (uint)indice >= (uint)_indiceInserccion) ThrowIndexOutOfRange(indice);
                return ref items[indice];
            }
        }

        #endregion

        #region Enumerable

        /// <summary>
        /// Retorna un enumerador de tipo struct sobre el búfer interno para permitir la iteración de la colección.
        /// Diseñado para ser detectado mediante duck-typing por el compilador en bucles foreach sin provocar boxing en el heap.
        /// </summary>
        /// <returns>Un enumerador de tipo struct de alto rendimiento (<see cref="Span{TItem}.Enumerator"/>) sobre los elementos activos.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<TItem>.Enumerator GetEnumerator()
            => Span.GetEnumerator();

        #endregion

        #region Dispose

        private bool _disposed;

        /// <summary>
        /// Obtiene un valor que indica si la lista ha sido devuelta al pool.
        /// </summary>
        public readonly bool EstaDisposed => _disposed;

        /// <summary>
        /// Libera los recursos de la estructura de forma síncrona y devuelve el búfer de memoria alquilado
        /// al <see cref="System.Buffers.ArrayPool{TItem}.Shared"/>.
        /// </summary>
        /// <remarks>
        /// Al ser un <see langword="ref struct"/>, está confinada al stack, por lo que no es posible que sea boxing
        /// ni se guarde en el heap, omitiendo cualquier necesidad de validación de no-boxing (<see cref="JCarrillo.AOT.Core.Extensiones.Boxing.BoxingExtensions.ValidarNoBoxeado{T}"/>).
        /// </remarks>
        public void Dispose()
        {
            if (_disposed) return;
            DisposePrivate();
        }

        private void DisposePrivate()
        {
            _disposed = true;
            if (_items != null)
            {
                _indiceInserccion = 0;
                ArrayPool<TItem>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
                _items = null;
            }
        }

        #endregion

        #region IntentarAmpliar

        /// <summary>
        /// Intenta ampliar la capacidad del búfer interno de la lista para albergar al menos el tamaño especificado.
        /// </summary>
        /// <param name="nuevoTamaño">El tamaño mínimo de elementos requerido en la lista.</param>
        /// <returns>
        /// <see langword="true"/> si el tamaño fue ampliado correctamente o si la capacidad actual ya cubre el requisito;
        /// de lo contrario, <see langword="false"/> si la estructura ya ha sido liberada.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntentarAmpliar(int nuevoTamaño)
        {
            if (_disposed) return false;
            if (_items!.Length >= nuevoTamaño) return true;

            int nuevaCapacidad = Math.Max(nuevoTamaño, _items.Length * 2);

            TItem[] nuevoArray = ArrayPool<TItem>.Shared.Rent(nuevaCapacidad);
            _items.AsSpan(0, _indiceInserccion).CopyTo(nuevoArray.AsSpan());

            ArrayPool<TItem>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());

            _items = nuevoArray;

            return true;
        }

        #endregion

        #region Helpers de Excepciones (Evitan contaminación del JIT)

        private const string NombreClase = "PooledListRef<" + nameof(TItem) + ">";

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

        #region Add

        /// <summary>
        /// Añade un elemento al final de la lista, ampliando automáticamente la capacidad del búfer interno si es necesario.
        /// </summary>
        /// <param name="item">El elemento que se va a añadir a la lista.</param>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si se intenta añadir un elemento después de que los recursos subyacentes hayan sido liberados.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TItem item)
        {
            if (_disposed) ThrowObjectDisposed();
            IntentarAmpliar(_indiceInserccion + 1);
            // IntentarAmpliar asegura que hay espacio suficiente, así que podemos añadir el elemento, tambien aumenta la variable _tamaño internamente.
            _items![_indiceInserccion++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ReadOnlySpan<TItem> items)
        {
            if (_disposed) ThrowObjectDisposed();
            IntentarAmpliar(_indiceInserccion + items.Length);
            items.CopyTo(_items!.AsSpan(_indiceInserccion));
            _indiceInserccion += items.Length;
        }

        #endregion

        #region Clear

        /// <summary>
        /// Restablece la lista vaciando todos los elementos de forma lógica.
        /// Si los elementos contienen referencias (o son tipos de referencia), limpia el contenido del búfer físico
        /// para no retener referencias en memoria y prevenir fugas (leak) del recolector de basura.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Se lanza si la lista ya ha sido liberada.
        /// </exception>
        public void Clear()
        {
            if (_disposed) ThrowObjectDisposed();
            if (RuntimeHelpers.IsReferenceOrContainsReferences<TItem>())
                Span.Clear();
            _indiceInserccion = 0;
        }

        #endregion

    }
}
