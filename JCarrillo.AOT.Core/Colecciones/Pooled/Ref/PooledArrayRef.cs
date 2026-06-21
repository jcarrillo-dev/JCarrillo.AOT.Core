using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Colecciones.Pooled.Ref
{
    public ref struct PooledArrayRef<TItem>
    {
        #region Constructor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PooledArrayRef(int capacidadInicial)
        {
            if (capacidadInicial <= 0) ThrowArgumentOutOfRange(capacidadInicial);
            _items = ArrayPool<TItem>.Shared.Rent(capacidadInicial);
            _tamaño = capacidadInicial;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PooledArrayRef(TItem[] items, int tamaño)
        {
            if (tamaño < 0 || tamaño > items.Length) ThrowArgumentOutOfRange(tamaño);
            _items = items;
            _tamaño = tamaño;
        }

        #endregion

        #region Tamaño

        private int _tamaño;

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
                if (_disposed || _items is null) ThrowObjectDisposed();
                return _items.AsSpan(0, _tamaño);
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
                if (items is null || (uint)indice >= (uint)_tamaño) ThrowIndexOutOfRange(indice);
                return ref items[indice];
            }
        }

        #endregion

        #region Enumerable

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<TItem>.Enumerator GetEnumerator()
            => Span.GetEnumerator();

        #endregion

        #region Disposed

        private bool _disposed = false;
        public readonly bool EstaDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _disposed;
        }

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
                _tamaño = 0;
                ArrayPool<TItem>.Shared.Return(_items, RuntimeHelpers.IsReferenceOrContainsReferences<TItem>());
                _items = null;
            }
        }

        #endregion

        #region IntentarAmpliar

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IntentarAmpliar(int _)
            => false;

        #endregion

        #region Helpers de Excepciones (Evitan contaminación del JIT)

        private const string NombreClase = "PooledArrayRef<" + nameof(TItem) + ">";

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

        public void Clear()
        {
            if (_disposed || _items is null) ThrowObjectDisposed();
            Span.Clear();
        }

        #endregion

    }
}
