using JCarrillo.AOT.Core.Extensiones.Boxing;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Colecciones.Pooled
{
    public record struct PooledList<TItem> : IPooledStruct<TItem>
    {

        #region Constructor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledList() : this(64) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledList(int capacidadInicial)
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
        internal PooledList(TItem[] items, int tamaño)
        {
            if (tamaño < 0 || tamaño > items.Length) ThrowArgumentOutOfRange(tamaño);

            _items = items;
            _indiceInserccion = tamaño;
            _disposed = false;
        }

        #endregion

        #region Tamaño

        private int _indiceInserccion;
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
        public readonly bool EsAmpliable
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return _esAmpliable; }
        }

        #endregion

        #region Datos

        private TItem[]? _items;

        public readonly Span<TItem> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                return _items!.AsSpan(0, _indiceInserccion);
            }
        }

        public readonly Memory<TItem> Memory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_disposed) ThrowObjectDisposed();
                return _items!.AsMemory(0, _indiceInserccion);
            }
        }

        #endregion

        #region Indice

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<TItem>.Enumerator GetEnumerator()
            => Span.GetEnumerator();

        #endregion

        #region Dispose

        private bool _disposed;
        public readonly bool EstaDisposed => _disposed;

        public void Dispose()
        {
            this.ValidarNoBoxeado();
            if (_disposed) return;
            DisposePrivate();
        }

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
                _indiceInserccion = 0;
                ArrayPool<TItem>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
                _items = null;
            }
        }

        #endregion

        #region IntentarAmpliar

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

        private const string NombreClase = "PooledList<" + nameof(TItem) + ">";

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TItem item)
        {
            if (_disposed) ThrowObjectDisposed();
            IntentarAmpliar(_indiceInserccion + 1);
            // IntentarAmpliar asegura que hay espacio suficiente, así que podemos añadir el elemento, tambien aumenta la variable _tamaño internamente.
            _items![_indiceInserccion++] = item;
        }

        #endregion

        #region Clear

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