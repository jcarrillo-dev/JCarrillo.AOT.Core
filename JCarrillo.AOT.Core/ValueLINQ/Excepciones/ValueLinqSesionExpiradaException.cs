using JCarrillo.AOT.Core.Diagnostico;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    /// <summary>
    /// Excepción que se lanza cuando se intenta acceder a una sesión de ValueLINQ que ha expirado o cuyo búfer subyacente ha sido reutilizado.
    /// </summary>
    /// <remarks>
    /// Inicializa una nueva instancia de la clase <see cref="ValueLinqSesionExpiradaException"/>.
    /// </remarks>
    /// <param name="idEsperado">El identificador de sesión esperado.</param>
    /// <param name="idObtenido">El identificador de sesión obtenido.</param>
    /// <param name="indice">El índice del slot.</param>
    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public sealed class ValueLinqSesionExpiradaException(long idEsperado, long idObtenido, int indice) : InvalidOperationException($"Operación inválida en ValueLINQ: La sesión del índice [{indice}] ha expirado o el buffer fue reutilizado. Se esperaba el token '{idEsperado}' pero el slot está ocupado por '{idObtenido}'.{JCEDiagnostico.Referencia(JCEDiagnostico.JCE0003.Url)}")
    {
        /// <summary>
        /// Obtiene el identificador de sesión esperado.
        /// </summary>
        public long IdEsperado { get; } = idEsperado;

        /// <summary>
        /// Obtiene el identificador de sesión obtenido actualmente en el slot.
        /// </summary>
        public long IdObtenido { get; } = idObtenido;

        /// <summary>
        /// Obtiene el índice del slot que causó la expiración.
        /// </summary>
        public int Indice { get; } = indice;
    }
}
