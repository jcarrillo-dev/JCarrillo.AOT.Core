#define TEST_DELAY_IMPLEMENTED
#if NET9_0_OR_GREATER && !TEST_DELAY_IMPLEMENTED

using System;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IValueLINQEnumerator<T>
    {
        bool MoveNext();
        ref readonly T Current { get; }
    }
}

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public ref struct ValueLINQDelayStruct<T, TEnumerator>
        where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
    {
        public ValueLINQDelayStruct<T, TEnumerator> Where<TState, TPredicate>(TState state, ref TPredicate predicate)
            where TPredicate : IWhereDelegado<T, TState>, allows ref struct
        {
            return this;
        }

        public ValueLINQDelayStruct<TResultado, DummyEnumerator<TResultado>> Select<TOrigen, TSelector, TResultado>(ref TSelector selector)
            where TSelector : ISelectDelegado<T, TResultado>, allows ref struct
        {
            return default;
        }

        public Enumerator GetEnumerator() => default;

        public ref struct Enumerator
        {
            public bool MoveNext() => false;
            public ref readonly T Current => ref _dummy;
            private static readonly T _dummy = default!;
        }
    }

    public ref struct DummyEnumerator<T> : IValueLINQEnumerator<T>
    {
        public bool MoveNext() => false;
        public ref readonly T Current => ref _dummy;
        private static readonly T _dummy = default!;
    }
}

namespace JCarrillo.AOT.Core.Extensiones.ValueLINQ
{
    using JCarrillo.AOT.Core.ValueLINQ;

    public static class ValueLINQDelayExtensions
    {
        public static ValueLINQDelayStruct<T, DummyEnumerator<T>> ToValueDelayQuery<T>(this ValueLINQStruct<T> query)
        {
            return default;
        }

        public static ValueLINQDelayStruct<T, DummyEnumerator<T>> ToValueDelayQuery<T>(this ValueLINQRefStruct<T> query)
        {
            return default;
        }

        public static ValueLINQDelayStruct<T, DummyEnumerator<T>> ToValueDelayQuery<T>(this T[] array)
        {
            return default;
        }

        public static ValueLINQDelayStruct<T, DummyEnumerator<T>> ToValueDelayQuery<T>(this Span<T> span)
        {
            return default;
        }

        public static ValueLINQDelayStruct<T, DummyEnumerator<T>> ToValueDelayQuery<T>(this ReadOnlySpan<T> span)
        {
            return default;
        }
    }
}

#endif
