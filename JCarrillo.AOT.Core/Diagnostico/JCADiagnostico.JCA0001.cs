#pragma warning disable IDE0055

namespace JCarrillo.AOT.Core.Diagnostico
{
    public static partial class JCADiagnostico
    {
        /// <summary>
        /// Diagnóstico JCA0001: Indica el uso de delegados Func de entrada que pueden causar allocations.
        /// </summary>
        public static class JCA0001
        {
            /// <summary>
            /// Identificador del diagnóstico JCA0001.
            /// </summary>
            public const string Id = "JCA0001";
            
            /// <summary>
            /// Mensaje de advertencia del diagnóstico JCA0001.
            /// </summary>
            public const string Mensaje = "Esta firma utiliza delegados Func de entrada y puede generar allocations en el heap. Para evitarlo, asegúrese de usar una expresión lambda estática (static) o los adaptadores basados en struct.";
            
            /// <summary>
            /// URL de ayuda en la wiki para el diagnóstico JCA0001.
            /// </summary>
            public const string Url = UrlBase + Id + ".md";
        }
    }
}
