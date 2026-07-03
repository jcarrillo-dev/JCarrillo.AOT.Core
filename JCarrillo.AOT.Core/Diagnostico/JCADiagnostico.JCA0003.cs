#pragma warning disable IDE0055

namespace JCarrillo.AOT.Core.Diagnostico
{
    public static partial class JCADiagnostico
    {
        /// <summary>
        /// Diagnóstico JCA0003: Indica el uso de params T[] en .NET 8.0 que genera allocations.
        /// </summary>
        public static class JCA0003
        {
            /// <summary>
            /// Identificador del diagnóstico JCA0003.
            /// </summary>
            public const string Id = "JCA0003";
            
            /// <summary>
            /// Mensaje de advertencia del diagnóstico JCA0003.
            /// </summary>
            public const string Mensaje = "En .NET 8.0 esta firma genera un array intermedio (Heap Allocation) debido al uso de 'params'. Se recomienda actualizar a .NET 9.0+ (donde se optimiza con ReadOnlySpan) o evitar el uso de 'params' en esta versión.";
            
            /// <summary>
            /// URL de ayuda en la wiki para el diagnóstico JCA0003.
            /// </summary>
            public const string Url = UrlBase + Id + ".md";
        }
    }
}
