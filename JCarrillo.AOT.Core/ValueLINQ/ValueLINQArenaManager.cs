using JCarrillo.AOT.Core.ValueLINQ.Arena;
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

        private static readonly ConcurrentBag<Action<int>> _liberadores = [];
        private static readonly ConcurrentBag<Func<int, bool>> _sondas = [];

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
            TokenHelper.EscribirToken(ref arenaCero.Token, TokenHelper.CrearTokenArena(0, 1));
        }

        [DoesNotReturn]
        private static void ThrowSinCapacidadArenas()
            => throw new InvalidOperationException(
                    $"Capacidad máxima de arenas de ValueLINQ alcanzada ({ValueLINQConfig.Arenas - 1} arenas simultáneas)."
                );

        internal static long Alquilar(bool persistente = false)
        {
            using ValueLINQSpinLock spinLock = new(ref _spinLock);

            if (_cuenta == 0)
                ThrowSinCapacidadArenas();

            int id = _idsLibres[_cabeza];
            _cabeza = (_cabeza + 1) % _idsLibres.Length;
            _cuenta--;

            ref EstadoArena estado = ref _arenas[id];
            long token = TokenHelper.CrearTokenArena(id, ++estado.Generacion);

            TokenHelper.EscribirToken(ref estado.Token, token);
            estado.IsPersistente = persistente;
            estado.UltimoUso = Stopwatch.GetTimestamp();

            return token;
        }

        internal static void Liberar(long tokenArena)
        {
            int id = TokenHelper.ObtenerIdTokenArena(tokenArena);

            if (id == 0)
                return;

            bool HasLiberado = false;

            using (new ValueLINQSpinLock(ref _spinLock))
            {
                ref EstadoArena estado = ref _arenas[id];

                if (TokenHelper.LeerToken(ref estado.Token) == tokenArena)
                {
                    TokenHelper.EscribirToken(ref estado.Token, 0L);
                    estado.UltimoUso = -1;
                    HasLiberado = true;
                }
            }

            if (!HasLiberado)
                return;

            try
            {
                foreach (Action<int> liberador in _liberadores)
                {
                    try
                    {
                        liberador(id);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error liberando las tablas de la arena {id} en ValueLINQArenaManager: {ex}");
                    }
                }
            }
            finally
            {
                using ValueLINQSpinLock spinLock = new(ref _spinLock);
                _idsLibres[_cola] = id;
                _cola = (_cola + 1) % _idsLibres.Length;
                _cuenta++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static long ObtenerGeneracion(int idArena)
            => TokenHelper.ObtenerGeneracionTokenArena(TokenHelper.LeerToken(ref _arenas[idArena].Token));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsArenaViva(long tokenArena)
            => TokenHelper.LeerToken(ref _arenas[TokenHelper.ObtenerIdTokenArena(tokenArena)].Token) == tokenArena;

        internal static bool IsArenaViva(int idArena)
            => TokenHelper.LeerToken(ref _arenas[idArena].Token) != 0L;

        internal static bool IsArenaVacia(int idArena)
        {
            foreach (Func<int, bool> sonda in _sondas)
                if (sonda(idArena))
                    return false;

            return true;
        }

        internal static void Registrar(Action<int> liberador, Func<int, bool> sonda)
        {
            _liberadores.Add(liberador);
            _sondas.Add(sonda);
        }
    }
}
