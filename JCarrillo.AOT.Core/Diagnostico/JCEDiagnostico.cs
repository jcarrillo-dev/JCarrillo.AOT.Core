namespace JCarrillo.AOT.Core.Diagnostico
{
    /// <summary>
    /// Catálogo centralizado de excepciones de ejecución de JCarrillo.AOT.Core.
    /// </summary>
    /// <remarks>
    /// Es el equivalente en ejecución de <see cref="JCADiagnostico"/>, que cubre los diagnósticos de compilación. Cada excepción del framework añade a su mensaje la dirección de su ficha, de modo que quien la recibe llegue a las causas, a la forma segura de evitarla y, cuando exista, a la escotilla que permite asumir el riesgo de forma explícita.
    /// </remarks>
    public static partial class JCEDiagnostico
    {
        /// <summary>
        /// URL base para la documentación de excepciones en el repositorio.
        /// </summary>
        public const string UrlBase = "https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/blob/main/docs/Diagnostico/JCE/";

        /// <summary>
        /// Construye el sufijo que las excepciones añaden a su mensaje para remitir a la ficha del código indicado.
        /// </summary>
        /// <param name="url">La dirección de la ficha del código.</param>
        /// <returns>El sufijo listo para concatenar al mensaje.</returns>
        public static string Referencia(string url) => $" Causas y alternativas: {url}";
    }
}
