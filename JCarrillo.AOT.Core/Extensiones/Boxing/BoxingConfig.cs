namespace JCarrillo.AOT.Core.Extensiones.Boxing
{
    /// <summary>
    /// Configuración de límites y offsets de la pila para validaciones de boxing.
    /// </summary>
    public static class BoxingConfig
    {
        /// <summary>
        /// Desplazamiento mínimo estimado para el límite inferior del stack (1 MB).
        /// </summary>
        public const int StackLowOffset = 1024 * 1024;       // 1 MB

        /// <summary>
        /// Desplazamiento máximo estimado para el límite superior del stack (16 MB).
        /// </summary>
        public const int StackHighOffset = 16 * 1024 * 1024;  // 16 MB
    }
}
