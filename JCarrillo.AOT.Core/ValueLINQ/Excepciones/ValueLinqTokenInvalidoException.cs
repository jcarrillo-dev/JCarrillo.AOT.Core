using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    /// <summary>
    /// Excepción que se lanza cuando se intenta utilizar un token de sesión inválido en ValueLINQ.
    /// </summary>
    /// <remarks>
    /// Inicializa una nueva instancia de la clase <see cref="ValueLinqTokenInvalidoException"/>.
    /// </remarks>
    /// <param name="tokenObtenido">El token de sesión obtenido.</param>
    /// <param name="indiceMapeado">El índice mapeado.</param>
    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public sealed class ValueLinqTokenInvalidoException(long tokenObtenido, int indiceMapeado) : InvalidOperationException($"Operación inválida en ValueLINQ: El token de sesión '{tokenObtenido}' no es válido para su procesamiento (mapea al índice [{indiceMapeado}]). Asegúrese de inicializar la estructura correctamente.")
    {
        /// <summary>
        /// Obtiene el token inválido obtenido.
        /// </summary>
        public long TokenObtenido { get; } = tokenObtenido;

        /// <summary>
        /// Obtiene el índice al que mapeaba el token inválido.
        /// </summary>
        public int IndiceMapeado { get; } = indiceMapeado;
    }
}
