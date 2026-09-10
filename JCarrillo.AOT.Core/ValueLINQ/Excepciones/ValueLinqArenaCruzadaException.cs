using JCarrillo.AOT.Core.Diagnostico;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    /// <summary>
    /// Excepción que se lanza cuando una operación combina sesiones de arenas distintas (acceso cruzado entre arenas).
    /// </summary>
    /// <remarks>
    /// Inicializa una nueva instancia de la clase <see cref="ValueLinqArenaCruzadaException"/>.
    /// </remarks>
    /// <param name="arenaEsperada">La arena del primer operando, a la que deben pertenecer todos los demás.</param>
    /// <param name="arenaEncontrada">La arena del operando que no coincide.</param>
    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public sealed class ValueLinqArenaCruzadaException(int arenaEsperada, int arenaEncontrada) : InvalidOperationException($"Operación inválida en ValueLINQ: acceso cruzado entre arenas. Se esperaban todos los operandos en la arena [{arenaEsperada}] (la del primer elemento), pero uno pertenece a la arena [{arenaEncontrada}]. No se pueden combinar sesiones de arenas distintas.{JCEDiagnostico.Referencia(JCEDiagnostico.JCE0001.Url)}")
    {
        /// <summary>
        /// Obtiene la arena esperada (la del primer operando).
        /// </summary>
        public int ArenaEsperada { get; } = arenaEsperada;

        /// <summary>
        /// Obtiene la arena del operando que violó la restricción.
        /// </summary>
        public int ArenaEncontrada { get; } = arenaEncontrada;
    }
}
