using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    internal ref struct ValueLINQSpinLock
    {
        private ref SpinLock _lock;
        private bool _lockTaken;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQSpinLock(ref SpinLock spinLock)
        {
            _lock = ref spinLock;
            _lockTaken = false;
            _lock.Enter(ref _lockTaken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_lockTaken)
                _lock.Exit(useMemoryBarrier: true);
        }
    }

}
