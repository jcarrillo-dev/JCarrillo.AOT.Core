#pragma warning disable IDE0055

namespace JCarrillo.AOT.Core.Diagnostico
{
    public static partial class JCADiagnostico
    {
        /// <summary>
        /// Diagnóstico JCA0002: Indica la materialización en colecciones estándar que generan allocations.
        /// </summary>
        public static class JCA0002
        {
            /// <summary>
            /// Identificador del diagnóstico JCA0002.
            /// </summary>
            public const string Id = "JCA0002";
            
            /// <summary>
            /// Mensaje de advertencia del diagnóstico JCA0002.
            /// </summary>
            public const string Mensaje = "Este método realiza la materialización a una colección estándar e induce asignaciones en el Heap (Allocations). Considere el uso de materializadores pooled (ToList, ToArray) para preservar el perfil zero-allocation.";
            
            /// <summary>
            /// URL de ayuda en la wiki para el diagnóstico JCA0002.
            /// </summary>
            public const string Url = UrlBase + Id + ".md";
        }
    }
}
