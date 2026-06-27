using System;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public ref struct ValueLINQEnumerator<T>
    {
        private readonly Span<T> _span;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQEnumerator(long token)
        {
            if (token == 0L)
                _span = Span<T>.Empty;
            else
            {
                ref var metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                _span = metadatos.Array.AsSpan(0, metadatos.TamañoActual);
            }
            _index = -1;
        }

        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            bool hasNext = next < _span.Length;
            if (hasNext)
                _index = next;
            return hasNext;
        }
    }
}
