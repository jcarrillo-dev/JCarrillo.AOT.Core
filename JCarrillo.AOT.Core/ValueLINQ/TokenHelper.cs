using System.Runtime.CompilerServices;


namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Clase auxiliar para la creación, lectura y manipulación de tokens de sesión en ValueLINQ.
    /// </summary>
    public static class TokenHelper
    {
        /// <summary>
        /// Crea un token de sesión a partir de un índice de ranura (slot) y una versión.
        /// </summary>
        /// <param name="slotIndex">El índice de la ranura de sesión.</param>
        /// <param name="version">La versión de la sesión.</param>
        /// <returns>El token de sesión generado como un valor <see cref="long"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CrearToken(int slotIndex, long version) => (version << 12) | (slotIndex & 0xFFFL);

        /// <summary>
        /// Obtiene el índice de la ranura (slot) codificado en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>El índice de la ranura correspondiente.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ObtenerSlotIndex(long token) => (int)(token & 0xFFFL);

        /// <summary>
        /// Obtiene la versión codificada en un token de sesión.
        /// </summary>
        /// <param name="token">El token de sesión.</param>
        /// <returns>La versión de la sesión.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ObtenerVersion(long token) => (long)((ulong)token >> 12);

        /// <summary>
        /// Lee un token de forma segura según la arquitectura del proceso (64-bit o 32-bit).
        /// </summary>
        /// <param name="ubicacion">La referencia a la variable que almacena el token.</param>
        /// <returns>El valor del token leído.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long LeerToken(ref long ubicacion)
            => Environment.Is64BitProcess
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
            if (Environment.Is64BitProcess)
                Volatile.Write(ref ubicacion, valor);
            else
                _ = Interlocked.Exchange(ref ubicacion, valor);
        }
    }
}
