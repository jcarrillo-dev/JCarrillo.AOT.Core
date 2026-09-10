using System;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Configuración global de ValueLINQ.
    /// </summary>
    public static class ValueLINQConfig
    {
        /// <summary>
        /// Tamaño de la tabla de sesiones.
        /// </summary>
        public const int TamañoTabla = Slots;

        /// <summary>
        /// Cantidad de bits que ocupa el identificador de arena en el token de sesión.
        /// </summary>
        public const int ArenaBits = 12;

        /// <summary>
        /// Cantidad de arenas de memoria simultáneas direccionables (incluida la arena ambiente 0).
        /// </summary>
        public const int Arenas = 1 << ArenaBits;

        /// <summary>
        /// Máscara para obtener el identificador de arena del token.
        /// </summary>
        public const long ArenaMask = Arenas - 1;

        /// <summary>
        /// Cantidad de bits que ocupa la generación de arena en el token de sesión (desambigua encarnaciones de un mismo id de arena).
        /// </summary>
        public const int ArenaGenBits = 12;

        /// <summary>
        /// Máscara para obtener la generación de arena del token de sesión.
        /// </summary>
        public const long ArenaGenMask = (1L << ArenaGenBits) - 1;

        /// <summary>
        /// Cantidad de bits que ocupa la versión de la sesión en el token (protección ABA intra-encarnación).
        /// </summary>
        public const int VersionBits = 64 - SlotBits - ArenaBits - ArenaGenBits;

        /// <summary>
        /// Cantidad de bits que ocupa el índice de ranura (slot) en el token de sesión.
        /// </summary>
        public const int SlotBits = 12;

        /// <summary>
        /// Cantidad total de slots de sesión direccionables por el token.
        /// </summary>
        public const int Slots = 1 << SlotBits;

        /// <summary>
        /// Máscara para obtener el slot index del token.
        /// </summary>
        public const long SlotMask = Slots - 1;

        /// <summary>
        /// Cantidad de bits que ocupa el desplazamiento (offset) dentro de una partición de la tabla.
        /// </summary>
        public const int SlotsParticionBits = 6;

        /// <summary>
        /// Cantidad de bits que ocupa el índice de partición dentro del índice global de slot.
        /// </summary>
        public const int ParticionBits = SlotBits - SlotsParticionBits;

        /// <summary>
        /// Cantidad de particiones de la tabla de sesiones.
        /// </summary>
        public const int Particiones = 1 << ParticionBits;

        /// <summary>
        /// Cantidad de slots contenidos en cada partición de la tabla.
        /// </summary>
        public const int SlotsEnParticion = 1 << SlotsParticionBits;

        /// <summary>
        /// Máscara para obtener el desplazamiento (offset) de un slot dentro de su partición.
        /// </summary>
        public const int SlotsParticionMask = SlotsEnParticion - 1;

        /// <summary>
        /// Tiempo mínimo de limpieza permitido.
        /// </summary>
        public static readonly TimeSpan TiempoLimpiezaMinimo = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Tiempo de limpieza por defecto.
        /// </summary>
        public static readonly TimeSpan TiempoLimpiezaPorDefecto = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Intervalo para la ejecución del periodic timer de GC.
        /// </summary>
        public static readonly TimeSpan IntervaloGC = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Intervalo mínimo entre refrescos de <c>UltimoAcceso</c> durante el consumo activo de una sesión.
        /// </summary>
        public static readonly TimeSpan TiempoRefrescoAcceso = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Tiempo que una arena no persistente debe permanecer vacía e inactiva antes de que la recolección automática la libere, cuando no se indica uno propio al crearla.
        /// </summary>
        public static readonly TimeSpan TiempoInactividadArena = TimeSpan.FromMinutes(5);
    }
}
