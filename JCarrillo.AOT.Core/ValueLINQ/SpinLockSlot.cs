using System.Runtime.InteropServices;
using System.Threading;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal struct SpinLockSlot
    {
        public SpinLock Lock;
    }
}
