using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    internal static class ValueLINQArenaManager
    {
        private static readonly EstadoArena[] _arenas = new EstadoArena[ValueLINQConfig.Arenas];

        private static SpinLock _spinLock = new(enableThreadOwnerTracking: false);

        private static readonly int[] _idsLibres = new int[ValueLINQConfig.Arenas - 1];
        private static int _cabeza;
        private static int _cola;
        private static int _cuenta;

        private static SpinLock _spinLockRegistro = new(enableThreadOwnerTracking: false);

        private static Action<int>[] _liberadores = [];
        private static Func<int, EstadoTabla>[] _sondas = [];

        static ValueLINQArenaManager()
        {
            for (int i = 0; i < ValueLINQConfig.Arenas; i++)
                _arenas[i].UltimoUso = -1;

            for (int id = 1; id < ValueLINQConfig.Arenas; id++)
                _idsLibres[id - 1] = id;

            _cabeza = 0;
            _cola = 0;
            _cuenta = ValueLINQConfig.Arenas - 1;

            ref EstadoArena arenaCero = ref _arenas[0];
            arenaCero.Generacion = 1;
            arenaCero.IsPersistente = true;
            arenaCero.UltimoUso = Stopwatch.GetTimestamp();
            arenaCero.InactividadTicks = ValueLINQConfig.TiempoInactividadArena.Ticks;
            TokenHelper.EscribirToken(ref arenaCero.Token, TokenHelper.CrearTokenArena(0, 1));

            ValueLINQGC.Registrar(() => _ = RecolectarArenas());
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowSinCapacidadArenas()
            => throw new InvalidOperationException(
                    $"Capacidad máxima de arenas de ValueLINQ alcanzada ({ValueLINQConfig.Arenas - 1} arenas simultáneas)."
                );

        internal static long Alquilar(bool persistente = false, TimeSpan? inactividad = null)
        {
            long inactividadTicks = (inactividad ?? ValueLINQConfig.TiempoInactividadArena).Ticks;

            if (HasAlquilado(persistente, inactividadTicks, out long token))
                return token;

            _ = RecolectarArenas();

            if (HasAlquilado(persistente, inactividadTicks, out token))
                return token;

            ThrowSinCapacidadArenas();
            return 0L;
        }

        private static bool HasAlquilado(bool persistente, long inactividadTicks, out long token)
        {
            using ValueLINQSpinLock spinLock = new(ref _spinLock);

            token = 0L;

            if (_cuenta == 0)
                return false;

            int id = _idsLibres[_cabeza];
            _cabeza = (_cabeza + 1) % _idsLibres.Length;
            _cuenta--;

            ref EstadoArena estado = ref _arenas[id];
            token = TokenHelper.CrearTokenArena(id, ++estado.Generacion);

            estado.IsPersistente = persistente;
            estado.InactividadTicks = inactividadTicks;
            estado.UltimoUso = Stopwatch.GetTimestamp();

            TokenHelper.EscribirToken(ref estado.Token, token);

            return true;
        }

        internal static void Liberar(long tokenArena)
            => _ = HasLiberado(TokenHelper.ObtenerIdTokenArena(tokenArena), tokenArena, confirmarVacia: false);

        private static bool HasLiberado(int id, long tokenArena, bool confirmarVacia)
        {
            if (id == 0)
                return false;

            bool hasMarcado = false;

            using (new ValueLINQSpinLock(ref _spinLock))
            {
                ref EstadoArena estado = ref _arenas[id];

                if (TokenHelper.LeerToken(ref estado.Token) == tokenArena && (!confirmarVacia || IsArenaVacia(id, out _)))
                {
                    TokenHelper.EscribirToken(ref estado.Token, 0L);
                    estado.UltimoUso = -1;
                    hasMarcado = true;
                }
            }

            if (!hasMarcado)
                return false;

            try
            {
                foreach (Action<int> liberador in Volatile.Read(ref _liberadores))
                    try
                    {
                        liberador(id);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error liberando las tablas de la arena {id} en ValueLINQArenaManager: {ex}");
                    }
            }
            finally
            {
                using ValueLINQSpinLock spinLock = new(ref _spinLock);
                _idsLibres[_cola] = id;
                _cola = (_cola + 1) % _idsLibres.Length;
                _cuenta++;
            }

            return true;
        }

        internal static int RecolectarArenas()
        {
            long ahora = Stopwatch.GetTimestamp();
            int recolectadas = 0;

            for (int id = 1; id < ValueLINQConfig.Arenas; id++)
            {
                ref EstadoArena estado = ref _arenas[id];
                long tokenArena = TokenHelper.LeerToken(ref estado.Token);

                if (tokenArena == 0L || estado.IsPersistente)
                    continue;

                if (!IsArenaVacia(id, out long vaciaDesde))
                    continue;

                long referencia = vaciaDesde != 0L ? vaciaDesde : Volatile.Read(ref estado.UltimoUso);

                if (referencia <= 0L || Stopwatch.GetElapsedTime(referencia, ahora).Ticks < estado.InactividadTicks)
                    continue;

                if (HasLiberado(id, tokenArena, confirmarVacia: true))
                    recolectadas++;
            }

            return recolectadas;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ObtenerGeneracion(int idArena)
            => TokenHelper.ObtenerGeneracionTokenArena(TokenHelper.LeerToken(ref _arenas[idArena].Token));

        internal static long TokenArenaDesdeSesion(long tokenSesion)
        {
            int idArena = TokenHelper.ObtenerArenaId(tokenSesion);

            if (idArena == 0)
                return 0L;

            long generacion = ObtenerGeneracion(idArena);

            if (generacion == 0L || (int)(generacion & ValueLINQConfig.ArenaGenMask) != TokenHelper.ObtenerArenaGen(tokenSesion))
                ThrowArenaInactiva(idArena);

            return TokenHelper.CrearTokenArena(idArena, generacion);
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowArenaInactiva(int idArena)
            => throw new ValueLinqArenaInactivaException(idArena);

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArenaCruzada(int arenaEsperada, int arenaEncontrada)
            => throw new ValueLinqArenaCruzadaException(arenaEsperada, arenaEncontrada);

        /// <summary>
        /// Acumula la arena explícita de un operando sobre la que ya llevan los anteriores, lanzando si aparece una segunda distinta.
        /// </summary>
        /// <param name="arenaAcumulada">La arena explícita vista hasta ahora, o cero si aún no apareció ninguna.</param>
        /// <param name="arena">El identificador de arena del operando que se combina.</param>
        /// <returns>La arena explícita que resulta de combinar ambas.</returns>
        /// <remarks>
        /// Es la regla única de mezcla de arenas para los dos motores. La arena ambiente vale cero y no impone ámbito: no se puede liberar y vive hasta el fin del proceso, así que sumarla no añade una vida útil que pueda expirar. Lo que se limita es la cantidad de arenas explícitas, que son las que sí mueren y dejarían la consulta a merced de la intersección de varias vidas. Acumular en vez de comparar contra el destino es lo que impide que una consulta ambiente admita operandos de arenas distintas entre sí.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CombinarArena(int arenaAcumulada, int arena)
        {
            if (arena == 0)
                return arenaAcumulada;

            if (arenaAcumulada == 0)
                return arena;

            if (arena != arenaAcumulada)
                ThrowArenaCruzada(arenaAcumulada, arena);

            return arenaAcumulada;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsArenaViva(long tokenArena)
            => TokenHelper.LeerToken(ref _arenas[TokenHelper.ObtenerIdTokenArena(tokenArena)].Token) == tokenArena;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsArenaViva(int idArena)
            => TokenHelper.LeerToken(ref _arenas[idArena].Token) != 0L;

        internal static bool IsArenaVacia(int idArena, out long vaciaDesde)
        {
            vaciaDesde = 0L;

            foreach (Func<int, EstadoTabla> sonda in Volatile.Read(ref _sondas))
            {
                EstadoTabla estado = sonda(idArena);

                if (estado.HasSesionesActivas)
                {
                    vaciaDesde = 0L;
                    return false;
                }

                if (estado.VaciaDesde > vaciaDesde)
                    vaciaDesde = estado.VaciaDesde;
            }

            return true;
        }

        internal static void Registrar(Action<int> liberador, Func<int, EstadoTabla> sonda)
        {
            using ValueLINQSpinLock spinLock = new(ref _spinLockRegistro);

            Volatile.Write(ref _liberadores, [.. _liberadores, liberador]);
            Volatile.Write(ref _sondas, [.. _sondas, sonda]);
        }
    }
}
