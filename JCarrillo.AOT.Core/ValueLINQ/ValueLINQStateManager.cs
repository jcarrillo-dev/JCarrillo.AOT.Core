using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Administrador de estado global para las consultas de ValueLINQ.
    /// Gestiona el ciclo de vida y la reutilización de los búferes de memoria.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos gestionados en el estado.</typeparam>
    public sealed class ValueLINQStateManager<T>
    {
        private static readonly TablaSesiones<T>?[] _tablas = new TablaSesiones<T>[ValueLINQConfig.Arenas];

        static ValueLINQStateManager()
        {
            _tablas[0] = new(0, ValueLINQArenaManager.ObtenerGeneracion(0));

            ValueLINQGC.Registrar(LimpiarExpirados);
            ValueLINQArenaManager.Registrar(LiberarTablasDeArena, HasSesionesVivasEnArena);
        }

        #region Limpieza

        private static readonly TimeSpan _tiempoLimpiezaMinimo = ValueLINQConfig.TiempoLimpiezaMinimo;

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowTiempoEntreLimpiezaInsuficiente(TimeSpan tiempoLimpieza, string paramName)
            => throw new ArgumentOutOfRangeException(
                paramName,
                tiempoLimpieza,
                $"Operación inválida en ValueLINQ: El intervalo configurado ({tiempoLimpieza.TotalSeconds}s) es insuficiente. Para prevenir la degradación del rendimiento por la recolección prematura de buffers activos, el tiempo mínimo permitido es de {_tiempoLimpiezaMinimo.TotalMinutes} minuto(s).");

        private static TimeSpan _tiempoLimpieza = ValueLINQConfig.TiempoLimpiezaPorDefecto;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TimeSpan GetTiempoLimpieza() => _tiempoLimpieza;

        /// <summary>
        /// Obtiene o establece el intervalo de tiempo para la limpieza de sesiones expiradas.
        /// </summary>
        [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Diseño heredado necesario para la gestión de estados por tipo.")]
        public static TimeSpan TiempoLimpieza
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetTiempoLimpieza();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value < _tiempoLimpiezaMinimo)
                    ThrowTiempoEntreLimpiezaInsuficiente(value, nameof(value));

                _tiempoLimpieza = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TablaSesiones<T> ObtenerOCrearTabla(int idArena)
        {
            TablaSesiones<T>? tbl = _tablas[idArena];

            if (tbl != null)
                return tbl;

            long generacion = ValueLINQArenaManager.ObtenerGeneracion(idArena);

            if (generacion == 0L)
                ThrowArenaInactiva(idArena);

            TablaSesiones<T> nueva = new(idArena, generacion);
            TablaSesiones<T>? previo = Interlocked.CompareExchange(ref _tablas[idArena], nueva, null);

            if (previo != null)
                return previo;

            if (ValueLINQArenaManager.ObtenerGeneracion(idArena) != generacion)
            {
                if (ReferenceEquals(Interlocked.CompareExchange(ref _tablas[idArena], null, nueva), nueva))
                    nueva.LiberarTodo();

                ThrowArenaInactiva(idArena);
            }

            return nueva;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TablaSesiones<T>? IntentarObtenerTabla(long token)
            => ValueLINQArenaManager.IsArenaViva(TokenHelper.ObtenerArenaId(token)) ? _tablas[TokenHelper.ObtenerArenaId(token)] : null;

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSesionNoEncontrada(long token)
            => throw new ValueLinqSesionExpiradaException(0L, token, TokenHelper.ObtenerSlotIndex(token));

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArenaInactiva(int idArena)
            => throw new ValueLinqArenaInactivaException(idArena);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TablaSesiones<T> ObtenerTabla(long token)
        {
            int idArena = TokenHelper.ObtenerArenaId(token);

            if (!ValueLINQArenaManager.IsArenaViva(idArena))
                ThrowArenaInactiva(idArena);

            TablaSesiones<T>? tbl = IntentarObtenerTabla(token);

            if (tbl == null)
                ThrowSesionNoEncontrada(token);

            return tbl;
        }

        private static void LimpiarExpirados()
        {
            foreach (TablaSesiones<T>? tabla in _tablas.AsSpan())
            {
                if (tabla == null)
                    continue;

                tabla.LimpiarSesionesExpiradas(TiempoLimpieza);
            }
        }

        #endregion

        internal static int SlotsLibres
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ObtenerOCrearTabla(0).IndicesLibres;
        }

        internal static void LiberarTablasDeArena(int id)
            => Interlocked.Exchange(ref _tablas[id], null)?.LiberarTodo();

        internal static bool HasSesionesVivasEnArena(int id)
            => _tablas[id]?.HasSesionesVivas ?? false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsTablaMaterializada(int idArena)
            => Volatile.Read(ref _tablas[idArena]) is not null;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref MetadatosSesion<T> ObtenerMetadatos(int idArena, int tamañoMinimo)
            => ref ObtenerOCrearTabla(idArena).ObtenerMetadatos(tamañoMinimo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref MetadatosSesion<T> ObtenerMetadatos(int tamañoMinimo)
            => ref ObtenerMetadatos(0, tamañoMinimo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref MetadatosSesion<T> ObtenerMetadatos(long token)
            => ref ObtenerTabla(token).ObtenerMetadatos(token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void AsegurarEspacio(long token, int tamañoMinimo)
            => ObtenerTabla(token).AsegurarEspacio(token, tamañoMinimo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RefrescarUltimoAcceso(long token, TimeSpan intervalo)
            => IntentarObtenerTabla(token)?.RefrescarUltimoAcceso(token, intervalo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Añadir(long token, T valor)
            => ObtenerTabla(token).Añadir(token, valor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Añadir(long token, ReadOnlySpan<T> span)
            => ObtenerTabla(token).Añadir(token, span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Añadir(long token, long otroToken)
            => ObtenerTabla(token).Añadir(token, otroToken);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void LiberarMetadatos(long token)
            => IntentarObtenerTabla(token)?.LiberarMetadatos(token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsMetadatoValido(long token)
            => IntentarObtenerTabla(token)?.IsMetadatoValido(token) ?? false;
    }
}
