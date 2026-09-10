using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Arena
{
    internal readonly struct EstadoTabla
    {
        internal readonly bool HasSesionesActivas;

        /// <summary>
        /// Marca de <see cref="System.Diagnostics.Stopwatch"/> del instante en que la tabla quedó completamente vacía, o cero si tiene sesiones activas o nunca se materializó.
        /// </summary>
        /// <remarks>
        /// El cero actúa como elemento neutro al agregar el estado de varios tipos con un máximo, de modo que una tabla inexistente no aporta ni bloquea. Es una marca temporal, no una duración.
        /// </remarks>
        internal readonly long VaciaDesde;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal EstadoTabla(bool hasSesionesActivas, long vaciaDesde)
        {
            HasSesionesActivas = hasSesionesActivas;
            VaciaDesde = vaciaDesde;
        }
    }
}
