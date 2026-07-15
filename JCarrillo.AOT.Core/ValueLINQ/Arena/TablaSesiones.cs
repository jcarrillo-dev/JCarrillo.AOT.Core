using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Arena
{
    internal sealed class TablaSesiones<T>
    {
        private readonly int _arenaId;
        private readonly long _arenaGen;
        private readonly int _capacidadMaxima;

        private readonly MetadatosSesion<T>[][] _datos = new MetadatosSesion<T>[ValueLINQConfig.Particiones][];
        private readonly SpinLockSlot[][] _spinLocks = new SpinLockSlot[ValueLINQConfig.Particiones][];

        #region Stack Indices

        private SpinLock _spinLockStack = new(enableThreadOwnerTracking: false);

        private readonly int[] _indicesLibresStack = new int[ValueLINQConfig.Slots];
        private int _topStack;

        internal int IndicesLibres => _topStack;

        internal bool TieneSesionesVivas => Volatile.Read(ref _topStack) < _capacidadMaxima;

        internal bool IsParticionMaterializada(int particion) => _datos[particion] is not null;

        private (int indice, int partition, int index) PopIndice()
        {
            using ValueLINQSpinLock spinLock = new(ref _spinLockStack);

            if (_topStack == 0)
                ThrowInvalidOperationSinCapacidad();

            int indice = _indicesLibresStack[--_topStack];
            (int particion, int index) = ObtenerIndiceParticion(indice);

            if (_datos[particion] is null)
            {
                MetadatosSesion<T>[] datos = new MetadatosSesion<T>[ValueLINQConfig.SlotsEnParticion];
                SpinLockSlot[] spinLocks = new SpinLockSlot[ValueLINQConfig.SlotsEnParticion];

                for (int i = 0; i < ValueLINQConfig.SlotsEnParticion; i++)
                {
                    ref SpinLockSlot slot = ref spinLocks[i];
                    ref MetadatosSesion<T> metadato = ref datos[i];

                    slot.Lock = new SpinLock(enableThreadOwnerTracking: false);

                    metadato.UltimoAcceso = -1;
                    metadato.IsDisposed = true;

                    TokenHelper.EscribirToken(ref metadato.Token, 0L);
                }

                Volatile.Write(ref _datos[particion], datos);
                Volatile.Write(ref _spinLocks[particion], spinLocks);
            }

            return (indice, particion, index);
        }

        private void PushIndice(int indice)
        {
            using ValueLINQSpinLock spinLock = new(ref _spinLockStack);

            _indicesLibresStack[_topStack++] = indice;
        }

        [DoesNotReturn]
        private static void ThrowInvalidOperationSinCapacidad()
            => throw new InvalidOperationException(
                    $"Capacidad máxima de ValueLINQ alcanzada ({ValueLINQConfig.Slots} buffers simultáneos para el tipo {typeof(T).Name})."
                );


        #endregion

        #region Constructor

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TablaSesiones(int arenaId) : this(arenaId, 1L, ValueLINQConfig.Slots) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TablaSesiones(int arenaId, long arenaGen) : this(arenaId, arenaGen, ValueLINQConfig.Slots) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TablaSesiones(int arenaId, long arenaGen, int capacidadMaxima)
        {
            _arenaId = arenaId;
            _arenaGen = arenaGen;
            _capacidadMaxima = capacidadMaxima;

            for (int i = capacidadMaxima - 1; i >= 0; i--)
            {
                _indicesLibresStack[i] = i;
            }

            _topStack = capacidadMaxima;
        }

        #endregion

        #region Throw

        [DoesNotReturn]
        private static void ThrowSesionExpirada(long idEsperado, long idObtenido, int indice)
            => throw new ValueLinqSesionExpiradaException(idEsperado, idObtenido, indice);

        [DoesNotReturn]
        private static void ThrowTokenInvalido(long idObtenido, int indice)
            => throw new ValueLinqTokenInvalidoException(idObtenido, indice);

        [DoesNotReturn]
        private static void ThrowSesionNoEncontrada(long token)
            => throw new ValueLinqSesionExpiradaException(0L, token, TokenHelper.ObtenerSlotIndex(token));

        #endregion

        #region Obtener Indice Particion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int partition, int index) ObtenerIndiceParticion(int indice)
        {
            int partition = (int)((uint)indice >> ValueLINQConfig.SlotsParticionBits);
            int index = indice & ValueLINQConfig.SlotsParticionMask;
            return (partition, index);
        }

        private static int ObtenerIndiceParticion(int partition, int index)
            => (partition << ValueLINQConfig.SlotsParticionBits) | index;

        #endregion

        #region Inicializar Metadatos

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InicializarMetadatos(ref MetadatosSesion<T> metadato, long token, int tamañoMinimo)
        {
            TokenHelper.EscribirToken(ref metadato.Token, token);

            metadato.Array = ArrayPool<T>.Shared.Rent(tamañoMinimo);
            metadato.TamañoActual = 0;
            metadato.IsDisposed = false;
            metadato.UltimoAcceso = Stopwatch.GetTimestamp();
        }

        #endregion

        #region Obtener Metadatos

        internal ref MetadatosSesion<T> ObtenerMetadatos(int tamañoMinimo)
        {
            (int indice, int particion, int index) = PopIndice();

            using ValueLINQSpinLock spinLock = new(ref _spinLocks[particion][index].Lock);

            ref MetadatosSesion<T> metadato = ref _datos[particion][index];
            long token = TokenHelper.CrearToken(indice, _arenaId, _arenaGen, ++metadato.Version);

            InicializarMetadatos(ref metadato, token, tamañoMinimo);

            return ref metadato;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref MetadatosSesion<T> ObtenerMetadatos(long token)
        {
            if (token == 0L)
                ThrowTokenInvalido(token, 0);

            int slotIndex = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(slotIndex);

            ref MetadatosSesion<T>[] metadato = ref _datos[particion];

            if (metadato is null)
                ThrowSesionNoEncontrada(token);

            ref MetadatosSesion<T> metadatoRef = ref metadato[index];

            if (TokenHelper.LeerToken(ref metadatoRef.Token) != token)
                ThrowSesionExpirada(TokenHelper.LeerToken(ref metadatoRef.Token), token, slotIndex);

            return ref metadatoRef;
        }

        #endregion

        #region Liberar Metadatos

        internal void LiberarMetadatos(long token)
        {
            if (token == 0L)
                return;

            int indice = TokenHelper.ObtenerSlotIndex(token);

            (int particion, int index) = ObtenerIndiceParticion(indice);

            ref SpinLockSlot[] spinLocks = ref _spinLocks[particion];

            if (spinLocks is null || _datos[particion] is null)
                return;

            T[]? arrayADevolver = null;
            bool HasLiberado = false;

            using (new ValueLINQSpinLock(ref spinLocks[index].Lock))
                HasLiberado = HasLiberadoMetadato(particion, index, token, out arrayADevolver);

            if (HasLiberado)
                PushIndice(indice);

            if (arrayADevolver is not null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasLiberadoMetadato(int particion, int index, long token, [NotNullWhen(true)] out T[]? arrayADevolver)
        {
            arrayADevolver = null;
            ref MetadatosSesion<T> metadato = ref _datos[particion][index];

            bool
                IsTokenCorrecto = TokenHelper.LeerToken(ref metadato.Token) == token,
                IsNotDisposed = !metadato.IsDisposed;

            return IsTokenCorrecto && IsNotDisposed && HasLimpiadoMetadatos(ref metadato, out arrayADevolver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasLimpiadoMetadatos(ref MetadatosSesion<T> metadatos, [NotNullWhen(true)] out T[]? array)
        {
            array = metadatos.Array;

            metadatos.Array = null;
            metadatos.TamañoActual = 0;
            metadatos.IsDisposed = true;
            metadatos.UltimoAcceso = -1;

            TokenHelper.EscribirToken(ref metadatos.Token, 0L);

            return array != null;
        }

        #endregion

        #region Es Metadato Valido

        internal bool IsMetadatoValido(long token)
        {
            if (token == 0L)
                return false;

            int slotIndex = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(slotIndex);

            ref MetadatosSesion<T>[] metadato = ref _datos[particion];

            if (metadato is null)
                return false;

            ref MetadatosSesion<T> metadatoRef = ref metadato[index];

            return
                TokenHelper.LeerToken(ref metadatoRef.Token) == token &&
                !metadatoRef.IsDisposed &&
                metadatoRef.Array != null;
        }

        #endregion

        #region Refrescar Ultimo Acceso

        internal void RefrescarUltimoAcceso(long token, TimeSpan intervalo)
        {
            if (token == 0L)
                return;

            int slotIndex = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(slotIndex);

            ref MetadatosSesion<T>[] metadato = ref _datos[particion];

            if (metadato is null)
                return;

            ref MetadatosSesion<T> metadatoRef = ref metadato[index];

            long ahora = Stopwatch.GetTimestamp();

            if (Stopwatch.GetElapsedTime(Volatile.Read(ref metadatoRef.UltimoAcceso), ahora) < intervalo)
                return;

            if (_spinLocks[particion] is null)
                return;

            using ValueLINQSpinLock spinLock = new(ref _spinLocks[particion][index].Lock);

            if (TokenHelper.LeerToken(ref metadatoRef.Token) != token || metadatoRef.IsDisposed)
                return;

            Volatile.Write(ref metadatoRef.UltimoAcceso, ahora);
        }

        #endregion

        #region Liberar Todo

        internal void LiberarTodo()
        {
            for (int particion = 0; particion < ValueLINQConfig.Particiones; particion++)
            {
                ref MetadatosSesion<T>[] datosParticion = ref _datos[particion];

                if (datosParticion is null)
                    continue;

                for (int index = 0; index < ValueLINQConfig.SlotsEnParticion; index++)
                {
                    ref MetadatosSesion<T> dato = ref datosParticion[index];

                    if (TokenHelper.LeerToken(ref dato.Token) == 0L || dato.UltimoAcceso == -1)
                        continue;

                    T[]? arrayADevolver = null;
                    bool HasLiberado = false;

                    using (new ValueLINQSpinLock(ref _spinLocks[particion][index].Lock))
                        HasLiberado = HasLiberadoMetadato(particion, index, TokenHelper.LeerToken(ref dato.Token), out arrayADevolver);

                    if (HasLiberado)
                        PushIndice(ObtenerIndiceParticion(particion, index));

                    if (arrayADevolver != null)
                        ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                }
            }
        }

        #endregion

        #region Limpiar Sesiones Expiradas

        private static bool IsLimpiezaRequerida(ref MetadatosSesion<T> metadato, long ahora, TimeSpan tiempoExpiracion)
            => metadato.UltimoAcceso != -1 && TokenHelper.LeerToken(ref metadato.Token) != 0L && !metadato.IsDisposed && Stopwatch.GetElapsedTime(Volatile.Read(ref metadato.UltimoAcceso), ahora) >= tiempoExpiracion;

        internal void LimpiarSesionesExpiradas(TimeSpan tiempoExpiracion)
        {
            long ahora = Stopwatch.GetTimestamp();

            using PooledListRef<(int, int)> sesionesCaducadas = new();

            for (int particion = 0; particion < ValueLINQConfig.Particiones; particion++)
            {
                ref MetadatosSesion<T>[] metadatosParticion = ref _datos[particion];

                if (metadatosParticion is null)
                    continue;

                for (int index = 0; index < ValueLINQConfig.SlotsEnParticion; index++)
                    if (IsLimpiezaRequerida(ref metadatosParticion[index], ahora, tiempoExpiracion))
                        sesionesCaducadas.Add((particion, index));
            }

            ahora = Stopwatch.GetTimestamp();

            foreach ((int particion, int index) in sesionesCaducadas.Span)
            {
                T[]? arrayADevolver = null;
                bool HasLiberado = false;

                using (new ValueLINQSpinLock(ref _spinLocks[particion][index].Lock))
                {
                    ref MetadatosSesion<T> metadato = ref _datos[particion][index];

                    if (IsLimpiezaRequerida(ref metadato, ahora, tiempoExpiracion))
                        HasLiberado = HasLiberadoMetadato(particion, index, TokenHelper.LeerToken(ref metadato.Token), out arrayADevolver);
                }

                if (HasLiberado)
                    PushIndice(ObtenerIndiceParticion(particion, index));

                if (arrayADevolver != null)
                    ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }

        #endregion

        #region Asegurar Espacio

        internal void AsegurarEspacio(long token, int tamañoMinimo)
        {
            if (token == 0)
                ThrowTokenInvalido(token, 0);

            int indice = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(indice);

            if (_spinLocks[particion] == null)
                ThrowSesionNoEncontrada(token);

            T[]? arrayADevolver = null;

            using (new ValueLINQSpinLock(ref _spinLocks[particion][index].Lock))
            {
                ref MetadatosSesion<T> datos = ref ObtenerMetadatos(token);

                if (datos.Array!.Length < tamañoMinimo)
                {
                    int nuevoTamaño = Math.Max(tamañoMinimo, datos.Array!.Length * 2);
                    T[] nuevoArray = ArrayPool<T>.Shared.Rent(nuevoTamaño);
                    arrayADevolver = datos.Array;

                    arrayADevolver.AsSpan(0, datos.TamañoActual).CopyTo(nuevoArray);
                    Volatile.Write(ref datos.Array, nuevoArray);
                }

                Volatile.Write(ref datos.UltimoAcceso, Stopwatch.GetTimestamp());
            }

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        #endregion

        #region Añadir

        internal void Añadir(long token, T valor)
        {
            if (token == 0L)
                ThrowTokenInvalido(token, 0);

            int indice = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(indice);

            ref SpinLockSlot[] spinlocks = ref _spinLocks[particion];

            if (spinlocks == null)
                ThrowSesionNoEncontrada(token);

            T[]? arrayADevolver = null;

            using (new ValueLINQSpinLock(ref spinlocks[index].Lock))
            {
                ref MetadatosSesion<T> metadatos = ref ObtenerMetadatos(token);
                int nuevoTamaño = metadatos.TamañoActual + 1;

                if (nuevoTamaño > metadatos.Array!.Length)
                {
                    nuevoTamaño = Math.Max(nuevoTamaño, metadatos.Array.Length * 2);
                    T[] nuevoArray = ArrayPool<T>.Shared.Rent(nuevoTamaño);
                    arrayADevolver = metadatos.Array;

                    arrayADevolver.AsSpan(0, metadatos.TamañoActual).CopyTo(nuevoArray);
                    Volatile.Write(ref metadatos.Array, nuevoArray);
                }

                metadatos.Array[metadatos.TamañoActual++] = valor;
                metadatos.UltimoAcceso = Stopwatch.GetTimestamp();
            }

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        internal void Añadir(long token, ReadOnlySpan<T> span)
        {
            if (token == 0L)
                ThrowTokenInvalido(token, 0);

            int indice = TokenHelper.ObtenerSlotIndex(token);
            (int particion, int index) = ObtenerIndiceParticion(indice);

            ref SpinLockSlot[] spinlocks = ref _spinLocks[particion];

            if (spinlocks == null)
                ThrowSesionNoEncontrada(token);

            T[]? arrayADevolver = null;

            using (new ValueLINQSpinLock(ref spinlocks[index].Lock))
            {
                ref MetadatosSesion<T> metadatos = ref ObtenerMetadatos(token);
                int nuevoTamaño = metadatos.TamañoActual + span.Length;

                if (nuevoTamaño > metadatos.Array!.Length)
                {
                    nuevoTamaño = Math.Max(nuevoTamaño, metadatos.Array.Length * 2);
                    T[] nuevoArray = ArrayPool<T>.Shared.Rent(nuevoTamaño);
                    arrayADevolver = metadatos.Array;

                    arrayADevolver.AsSpan(0, metadatos.TamañoActual).CopyTo(nuevoArray);
                    Volatile.Write(ref metadatos.Array, nuevoArray);
                }

                span.CopyTo(metadatos.Array.AsSpan(metadatos.TamañoActual));
                metadatos.TamañoActual += span.Length;
                metadatos.UltimoAcceso = Stopwatch.GetTimestamp();
            }

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
        internal void Añadir(long token, long otroToken)
        {
            if (token == 0L)
                ThrowTokenInvalido(token, 0);

            if (otroToken == 0L)
                return;

            int indice = TokenHelper.ObtenerSlotIndex(token);
            int otroIndice = TokenHelper.ObtenerSlotIndex(otroToken);

            (int particion, int index) = ObtenerIndiceParticion(indice);
            (int otroParticion, int otroIndex) = ObtenerIndiceParticion(otroIndice);

            if (_spinLocks[particion] == null)
                ThrowSesionNoEncontrada(token);

            if (_spinLocks[otroParticion] == null)
                ThrowSesionNoEncontrada(otroToken);

            T[]? arrayADevolver = null;

            ref SpinLock ObtenerSpinLock(int indice)
            {
                (int p, int i) = ObtenerIndiceParticion(indice);
                return ref _spinLocks[p][i].Lock;
            }

            ref SpinLock
                minSL = ref ObtenerSpinLock(Math.Min(indice, otroIndice)),
                maxSL = ref ObtenerSpinLock(Math.Max(indice, otroIndice));

            if (indice == otroIndice)
                using (new ValueLINQSpinLock(ref minSL))
                    AñadirInterno(token, otroToken, ref arrayADevolver);
            else
                using (new ValueLINQSpinLock(ref minSL))
                using (new ValueLINQSpinLock(ref maxSL))
                    AñadirInterno(token, otroToken, ref arrayADevolver);

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AñadirInterno(long token, long otroToken, ref T[]? arrayADevolver)
        {
            ref MetadatosSesion<T> metadatosOtro = ref ObtenerMetadatos(otroToken);
            if (metadatosOtro.TamañoActual == 0)
                return;

            ref MetadatosSesion<T> metadatos = ref ObtenerMetadatos(token);
            int nuevoTamañoRequerido = metadatos.TamañoActual + metadatosOtro.TamañoActual;

            if (nuevoTamañoRequerido > metadatos.Array!.Length)
            {
                int nuevoTamaño = Math.Max(nuevoTamañoRequerido, metadatos.Array.Length * 2);
                T[] nuevoArray = ArrayPool<T>.Shared.Rent(nuevoTamaño);
                arrayADevolver = metadatos.Array;

                arrayADevolver.AsSpan(0, metadatos.TamañoActual).CopyTo(nuevoArray);
                Volatile.Write(ref metadatos.Array, nuevoArray);
            }

            metadatosOtro.Array.AsSpan(0, metadatosOtro.TamañoActual).CopyTo(metadatos.Array.AsSpan(metadatos.TamañoActual));
            metadatos.TamañoActual += metadatosOtro.TamañoActual;
            metadatos.UltimoAcceso = Stopwatch.GetTimestamp();
        }

        #endregion
    }
}
