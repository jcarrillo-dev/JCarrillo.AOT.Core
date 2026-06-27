using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    internal class ValueLINQStateManager<T>
    {
        private const int _tamañoTabla = 4096;
        private const int _mascara = _tamañoTabla - 1;

        private static readonly MetadatosSesion<T>[] _entradas = new MetadatosSesion<T>[_tamañoTabla];
        private static readonly int[] _indicesLibresStack = new int[_tamañoTabla];
        private static int _topStack;
        private static readonly long[] _versiones = new long[_tamañoTabla];

        private static readonly object _stackRoot = new();
        private static readonly object[] _slotLocks = new object[_tamañoTabla];

        static ValueLINQStateManager()
        {
            for (int i = 0; i < _tamañoTabla; i++)
            {
                _slotLocks[i] = new object();
                _indicesLibresStack[i] = i;
                _entradas[i].UltimoAcceso = -1;
                _entradas[i].IsDisposed = true;
                TokenHelper.EscribirToken(ref _entradas[i].Token, 0L);
            }
            _topStack = _tamañoTabla;

            // Forzamos la ejecución asíncrona inmediata fuera del inicializador estático
            _ = Task.Run(async () =>
            {
                await Task.Yield();
                await LimpiezaPeriodicTimer();
            });
        }

        #region Limpieza

        private static TimeSpan _tiempoLimpiezaMinimo = TimeSpan.FromMinutes(1);
        private static TimeSpan _tiempoLimpieza = TimeSpan.FromMinutes(5);

        [DoesNotReturn]
        private static void ThrowTiempoEntreLimpiezaInsuficiente(TimeSpan tiempo)
            => throw new ArgumentOutOfRangeException(
                nameof(TiempoLimpieza),
                tiempo,
                $"Operación inválida en ValueLINQ: El intervalo configurado ({tiempo.TotalSeconds}s) es insuficiente. Para prevenir la degradación del rendimiento por la recolección prematura de buffers activos, el tiempo mínimo permitido es de {_tiempoLimpiezaMinimo.TotalMinutes} minuto(s).");

        public static TimeSpan TiempoLimpieza
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _tiempoLimpieza;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value < _tiempoLimpiezaMinimo)
                    ThrowTiempoEntreLimpiezaInsuficiente(value);

                _tiempoLimpieza = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ObtenerTicksDesdeTimeSpan(TimeSpan timeSpan)
        {
            // Transforma el TimeSpan a la escala nativa del hardware del sistema actual
            return (long)(timeSpan.TotalSeconds * Stopwatch.Frequency);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsLimpiezaRequerida(int indice, long tiempoInicio, TimeSpan tiempoMaximo)
        {
            ref var entrada = ref _entradas[indice];
            long token = TokenHelper.LeerToken(ref entrada.Token);

            if (token == 0L || entrada.UltimoAcceso == -1 || entrada.IsDisposed)
                return false;

            return Stopwatch.GetElapsedTime(entrada.UltimoAcceso, tiempoInicio) >= tiempoMaximo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LimpiezaFinal(ReadOnlyMemory<int> indices, long tiempoInicio, TimeSpan tiempoMaximo)
        {
            ReadOnlySpan<int> ints = indices.Span;
            for (int i = 0; i < indices.Length; i++)
            {
                int indice = ints[i];
                T[]? arrayADevolver = null;
                bool hasLiberado = false;

                lock (_slotLocks[indice])
                    if (IsLimpiezaRequerida(indice, tiempoInicio, tiempoMaximo))
                        hasLiberado = HasLiberadoMetadato(indice, TokenHelper.LeerToken(ref _entradas[indice].Token), out arrayADevolver);

                if (hasLiberado)
                    lock (_stackRoot)
                        PushIndice(indice);

                if (arrayADevolver != null)
                    ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private static async ValueTask LimpiezaPeriodicTimer()
        {
            using (var timer = new PeriodicTimer(TimeSpan.FromMinutes(1)))
            using (var candidatosALiberar = new PooledList<int>())
                while (await timer.WaitForNextTickAsync())
                    try
                    {
                        long timeSpanActual = Stopwatch.GetTimestamp();
                        TimeSpan tiempoLimpieza = _tiempoLimpieza;
                        candidatosALiberar.Clear();

                        for (int i = 0; i < _tamañoTabla; i++)
                            if (IsLimpiezaRequerida(i, timeSpanActual, tiempoLimpieza))
                                candidatosALiberar.Add(i);

                        if (candidatosALiberar.Tamaño == 0)
                            continue;

                        LimpiezaFinal(candidatosALiberar.Memory, Stopwatch.GetTimestamp(), tiempoLimpieza);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error en la limpieza periódica de ValueLINQ: {ex}");
                    }
        }

        #endregion

        #region Procesamiento de indices

        [DoesNotReturn]
        private static void ThrowInvalidOperationSinCapacidad()
            => throw new InvalidOperationException(
                    $"Capacidad máxima de ValueLINQ alcanzada ({_tamañoTabla} buffers simultáneos para el tipo {typeof(T).Name})."
                );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopIndice()
        {
            if (_topStack <= 0)
                ThrowInvalidOperationSinCapacidad();

            return _indicesLibresStack[--_topStack];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PushIndice(int indice)
        {
            _indicesLibresStack[_topStack++] = indice;
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref MetadatosSesion<T> ObtenerMetadatos(int tamañoMinimo)
        {
            int indice;
            lock (_stackRoot)
                indice = PopIndice();

            lock (_slotLocks[indice])
            {
                long version = ++_versiones[indice];
                long token = TokenHelper.CrearToken(indice, version);
                ref MetadatosSesion<T> metadatos = ref _entradas[indice];
                InicializarMetadatos(ref metadatos, token, tamañoMinimo);
                return ref metadatos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InicializarMetadatos(ref MetadatosSesion<T> metadatos, long token, int tamañoMinimo)
        {
            TokenHelper.EscribirToken(ref metadatos.Token, token);
            metadatos.Array = ArrayPool<T>.Shared.Rent(tamañoMinimo);
            metadatos.TamañoActual = 0;
            metadatos.IsDisposed = false;
            metadatos.UltimoAcceso = Stopwatch.GetTimestamp();
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSesionExpirada(long idEsperado, long idObtenido, int indice)
            => throw new ValueLinqSesionExpiradaException(idEsperado, idObtenido, indice);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTokenInvalido(long idObtenido, int indice)
            => throw new ValueLinqTokenInvalidoException(idObtenido, indice);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref MetadatosSesion<T> ObtenerMetadatos(long token)
        {
            if (token == 0L)
                ThrowTokenInvalido(token, 0);

            int indice = TokenHelper.ObtenerSlotIndex(token);
            ref MetadatosSesion<T> metadatos = ref _entradas[indice];

            long tokenActual = TokenHelper.LeerToken(ref metadatos.Token);
            bool isTokenCorrecto = tokenActual == token;

            if (!isTokenCorrecto)
                ThrowSesionExpirada(tokenActual, token, indice);

            return ref metadatos;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AsegurarEspacio(long token, int tamañoMinimo)
        {
            int indice = TokenHelper.ObtenerSlotIndex(token);
            T[]? arrayADevolver = null;

            lock (_slotLocks[indice])
            {
                ref MetadatosSesion<T> metadatos = ref ObtenerMetadatos(token);

                if (tamañoMinimo > metadatos.Array!.Length)
                {
                    int nuevoTamaño = Math.Max(tamañoMinimo, metadatos.Array.Length * 2);
                    T[] nuevoArray = ArrayPool<T>.Shared.Rent(nuevoTamaño);
                    arrayADevolver = metadatos.Array;

                    arrayADevolver.AsSpan(0, metadatos.TamañoActual).CopyTo(nuevoArray);
                    Volatile.Write(ref metadatos.Array, nuevoArray);
                }

                metadatos.UltimoAcceso = Stopwatch.GetTimestamp();
            }

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        #region Liberacion de metadatos

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LiberarMetadatos(long token)
        {
            if (token == 0L)
                return;

            int indice = TokenHelper.ObtenerSlotIndex(token);
            T[]? arrayADevolver = null;
            bool hasLiberado = false;

            lock (_slotLocks[indice])
                hasLiberado = HasLiberadoMetadato(indice, token, out arrayADevolver);

            if (hasLiberado)
                lock (_stackRoot)
                    PushIndice(indice);

            if (arrayADevolver != null)
                ArrayPool<T>.Shared.Return(arrayADevolver, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasLiberadoMetadato(int indice, long token, [NotNullWhen(true)] out T[]? arrayADevolver)
        {
            arrayADevolver = null;
            ref var entrada = ref _entradas[indice];

            bool isTokenCorrecto = TokenHelper.LeerToken(ref entrada.Token) == token;
            bool isNotDisposed = !entrada.IsDisposed;

            if (!isTokenCorrecto)
                return false;

            if (!isNotDisposed)
                return false;

            return HasLimpiadoMetadatos(ref entrada, out arrayADevolver);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasLimpiadoMetadatos(ref MetadatosSesion<T> metadatos, [NotNullWhen(true)] out T[]? array)
        {
            array = metadatos.Array;
            bool hasArray = array != null;

            metadatos.Array = null;
            metadatos.TamañoActual = 0;
            TokenHelper.EscribirToken(ref metadatos.Token, 0L);
            metadatos.IsDisposed = true;
            metadatos.UltimoAcceso = -1;

            return hasArray;
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsMetadatoValido(long token)
        {
            if (token == 0L)
                return false;

            int indice = TokenHelper.ObtenerSlotIndex(token);
            ref MetadatosSesion<T> entrada = ref _entradas[indice];

            bool isTokenCorrecto = TokenHelper.LeerToken(ref entrada.Token) == token;
            bool isNotDisposed = !entrada.IsDisposed;
            bool hasArray = entrada.Array != null;

            if (!isTokenCorrecto)
                return false;

            if (!isNotDisposed)
                return false;

            if (!hasArray)
                return false;

            return true;
        }
    }
}
