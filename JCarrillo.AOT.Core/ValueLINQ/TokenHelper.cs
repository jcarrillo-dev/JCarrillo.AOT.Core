using System.Runtime.CompilerServices;


namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Clase auxiliar para la creación, lectura y manipulación de tokens de sesión en ValueLINQ.
    /// </summary>
    public static class TokenHelper
    {
        private static readonly bool Is64BitOMas = IntPtr.Size >= 8;

        private const int ArenaShift = ValueLINQConfig.SlotBits;
        private const int ArenaGenShift = ValueLINQConfig.SlotBits + ValueLINQConfig.ArenaBits;
        private const int VersionShift = ValueLINQConfig.SlotBits + ValueLINQConfig.ArenaBits + ValueLINQConfig.ArenaGenBits;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizarVersion(long version)
            => version << VersionShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizarArenaGen(long arenaGen)
            => (arenaGen & ValueLINQConfig.ArenaGenMask) << ArenaGenShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizarArenaId(int arenaId)
            => (arenaId & ValueLINQConfig.ArenaMask) << ArenaShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizarSlotIndex(int slotIndex)
            => slotIndex & ValueLINQConfig.SlotMask;

        /// <summary>
        /// Crea un token de sesión a partir de un índice de ranura (slot), un identificador de arena, la generación de la arena y una versión.
        /// </summary>
        /// <param name="slotIndex">El índice de la ranura de sesión.</param>
        /// <param name="arenaId">El identificador de la arena propietaria de la sesión.</param>
        /// <param name="arenaGen">La generación de la arena propietaria (desambigua encarnaciones de un mismo id).</param>
        /// <param name="version">La versión de la sesión.</param>
        /// <returns>El token de sesión generado como un valor <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearToken(int slotIndex, int arenaId, long arenaGen, long version)
            => NormalizarVersion(version) | NormalizarArenaGen(arenaGen) | NormalizarArenaId(arenaId) | NormalizarSlotIndex(slotIndex);

        /// <summary>
        /// Crea un token de sesión a partir de un índice de ranura (slot), un identificador de arena y una versión, con generación de arena cero.
        /// </summary>
        /// <param name="slotIndex">El índice de la ranura de sesión.</param>
        /// <param name="arenaId">El identificador de la arena propietaria de la sesión.</param>
        /// <param name="version">La versión de la sesión.</param>
        /// <returns>El token de sesión generado como un valor <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearToken(int slotIndex, int arenaId, long version)
            => CrearToken(slotIndex, arenaId, 0L, version);

        /// <summary>
        /// Crea un token de sesión a partir de un índice de ranura (slot) y una versión, en la arena ambiente (id y generación cero).
        /// </summary>
        /// <param name="slotIndex">El índice de la ranura de sesión.</param>
        /// <param name="version">La versión de la sesión.</param>
        /// <returns>El token de sesión generado como un valor <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearToken(int slotIndex, long version)
            => CrearToken(slotIndex, 0, 0L, version);

        /// <summary>
        /// Obtiene el índice de la ranura (slot) codificado en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>El índice de la ranura correspondiente.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerSlotIndex(long token) => (int)(token & ValueLINQConfig.SlotMask);

        /// <summary>
        /// Obtiene el identificador de la arena codificado en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>El identificador de la arena correspondiente.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerArenaId(long token) => (int)(((ulong)token >> ArenaShift) & ValueLINQConfig.ArenaMask);

        /// <summary>
        /// Obtiene la generación de arena codificada en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>La generación de arena (los bits bajos) de la sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerArenaGen(long token) => (int)(((ulong)token >> ArenaGenShift) & ValueLINQConfig.ArenaGenMask);

        /// <summary>
        /// Obtiene la versión codificada en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>La versión de la sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ObtenerVersion(long token) => (long)((ulong)token >> VersionShift);





        /// <summary>
        /// Crea un token de arena a partir de un identificador de arena y una generación.
        /// </summary>
        /// <param name="idArena">El identificador de la arena.</param>
        /// <param name="generacion">La generación de la arena.</param>
        /// <returns>El token de arena generado como un valor <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearTokenArena(int idArena, long generacion)
            => (generacion << ValueLINQConfig.ArenaBits) | (idArena & ValueLINQConfig.ArenaMask);

        /// <summary>
        /// Obtiene el identificador de arena codificado en un token de arena.
        /// </summary>
        /// <param name="tokenArena">El token de arena.</param>
        /// <returns>El identificador de la arena correspondiente.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerIdTokenArena(long tokenArena) => (int)(tokenArena & ValueLINQConfig.ArenaMask);

        /// <summary>
        /// Obtiene la generación codificada en un token de arena.
        /// </summary>
        /// <param name="tokenArena">El token de arena.</param>
        /// <returns>La generación de la arena.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ObtenerGeneracionTokenArena(long tokenArena) => (long)((ulong)tokenArena >> ValueLINQConfig.ArenaBits);

        /// <summary>
        /// Lee un token de forma segura según la arquitectura del proceso (64-bit o 32-bit).
        /// </summary>
        /// <param name="ubicacion">La referencia a la variable que almacena el token.</param>
        /// <returns>El valor del token leído.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LeerToken(ref long ubicacion)
            => Is64BitOMas
                ? Volatile.Read(ref ubicacion)
                : Interlocked.Read(ref ubicacion);

        /// <summary>
        /// Escribe un token de forma segura según la arquitectura del proceso (64-bit o 32-bit).
        /// </summary>
        /// <param name="ubicacion">La referencia a la variable donde se escribirá el token.</param>
        /// <param name="valor">El valor del token a escribir.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EscribirToken(ref long ubicacion, long valor)
        {
            if (Is64BitOMas)
                Volatile.Write(ref ubicacion, valor);
            else
                _ = Interlocked.Exchange(ref ubicacion, valor);
        }
    }
}
