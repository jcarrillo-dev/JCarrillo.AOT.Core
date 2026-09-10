#if NET9_0_OR_GREATER
namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Renuncias explícitas que una canalización perezosa puede llevar activadas.
    /// </summary>
    /// <remarks>
    /// Se modela como conjunto y no como indicadores sueltos porque, con el relleno de la estructura, una bandera cuesta lo mismo que ocho, y así la pregunta de qué combinaciones son legales queda explícita cuando aparezca la segunda.
    /// </remarks>
    [Flags]
    internal enum ValueLINQDelayFlags : byte
    {
        /// <summary>
        /// Sin renuncias: la canalización aplica todas las comprobaciones.
        /// </summary>
        Ninguna = 0,

        /// <summary>
        /// Omite el recuento de arenas explícitas entre los operandos de las operaciones que combinan canalizaciones.
        /// </summary>
        SinComprobarLimitesDeArena = 1
    }
}
#endif
