using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Excepciones
{
    /// <summary>
    /// Excepción que se lanza cuando se intenta crear una sesión en una arena que no está activa (nunca fue creada o ya fue liberada).
    /// </summary>
    /// <remarks>
    /// Inicializa una nueva instancia de la clase <see cref="ValueLinqArenaInactivaException"/>.
    /// </remarks>
    /// <param name="idArena">El identificador de la arena inactiva.</param>
    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public sealed class ValueLinqArenaInactivaException(int idArena) : InvalidOperationException($"Operación inválida en ValueLINQ: La arena [{idArena}] no está activa. Fue liberada o nunca se creó; obtenga una arena válida mediante ValueLINQArena.Crear() antes de usarla.")
    {
        /// <summary>
        /// Obtiene el identificador de la arena inactiva.
        /// </summary>
        public int IdArena { get; } = idArena;
    }
}
