using System;
using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ.Arena
{
    /// <summary>
    /// Ámbito de memoria explícito para consultas de ValueLINQ. Todas las sesiones creadas dentro de la arena
    /// se liberan de golpe al disponerla, incluidas las que el usuario olvide liberar individualmente.
    /// </summary>
    /// <remarks>
    /// Es un <see langword="readonly struct"/> de 8 bytes: solo transporta el token de arena (id + generación).
    /// El almacenamiento real vive en las tablas de sesión por tipo del <see cref="ValueLINQStateManager{T}"/>,
    /// enrutadas por el id de arena del token.
    /// </remarks>
    public readonly struct ValueLINQArena : IDisposable, IEquatable<ValueLINQArena>
    {
        internal readonly long TokenArena;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueLINQArena(long tokenArena) => TokenArena = tokenArena;

        /// <summary>
        /// Obtiene el identificador numérico de la arena codificado en su token.
        /// </summary>
        public readonly int Id
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TokenHelper.ObtenerIdTokenArena(TokenArena);
        }

        /// <summary>
        /// Obtiene un valor que indica si la arena sigue activa (no ha sido liberada).
        /// </summary>
        public readonly bool IsViva
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ValueLINQArenaManager.IsArenaViva(TokenArena);
        }

        /// <summary>
        /// Crea una nueva arena de memoria para ValueLINQ.
        /// </summary>
        /// <param name="persistente">
        /// Excluye la arena de la recolección automática por inactividad. Una arena guardada en un campo de larga
        /// duración debe crearse con este indicador, porque el criterio de recolección no distingue una arena olvidada
        /// de una viva que simplemente lleva un rato sin tráfico.
        /// </param>
        /// <param name="inactividad">
        /// Tiempo que la arena debe permanecer vacía antes de que la recolección automática la libere. Si se omite se
        /// usa <see cref="ValueLINQConfig.TiempoInactividadArena"/>. <see cref="TimeSpan.Zero"/> y cualquier valor
        /// negativo se comportan igual: hacen la arena recolectable en el primer barrido tras vaciarse (la comparación
        /// de recolección es <c>elapsed.Ticks &lt; InactividadTicks</c>, que con cero o negativo nunca salta). Para
        /// indicar que no se recolecte nunca use <paramref name="persistente"/> en <see langword="true"/>, no un umbral
        /// bajo. No tiene efecto sobre una arena persistente.
        /// </param>
        /// <returns>Una nueva <see cref="ValueLINQArena"/> activa.</returns>
        /// <remarks>
        /// La arena debe disponerse explícitamente. La recolección automática solo alcanza a las arenas no persistentes
        /// que quedan vacías, así que no sustituye al <see cref="Dispose"/>: agotar los identificadores disponibles hace
        /// fallar la creación de nuevas arenas.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQArena Crear(bool persistente = false, TimeSpan? inactividad = null)
            => new(ValueLINQArenaManager.Alquilar(persistente, inactividad));

        /// <summary>
        /// Libera la arena y todas las sesiones de ValueLINQ creadas dentro de ella, en cualquier tipo.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose()
            => ValueLINQArenaManager.Liberar(TokenArena);

        /// <summary>
        /// Determina si la arena actual es igual a otra <see cref="ValueLINQArena"/>.
        /// </summary>
        /// <param name="other">La otra arena con la que se comparará.</param>
        /// <returns><see langword="true"/> si ambas arenas poseen el mismo token; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(ValueLINQArena other)
            => TokenArena == other.TokenArena;

        /// <summary>
        /// Determina si el objeto especificado es una instancia de <see cref="ValueLINQArena"/> y es igual a la actual.
        /// </summary>
        /// <param name="obj">El objeto a comparar con la arena actual.</param>
        /// <returns><see langword="true"/> si el objeto especificado es un <see cref="ValueLINQArena"/> con el mismo token; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object? obj)
            => obj is ValueLINQArena other && Equals(other);

        /// <summary>
        /// Devuelve el código hash para esta instancia a partir de su token de arena.
        /// </summary>
        /// <returns>Un código hash entero de 32 bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode()
            => TokenArena.GetHashCode();

        /// <summary>
        /// Determina si dos instancias de <see cref="ValueLINQArena"/> son iguales.
        /// </summary>
        /// <param name="left">Primera arena a comparar.</param>
        /// <param name="right">Segunda arena a comparar.</param>
        /// <returns><see langword="true"/> si ambas instancias poseen el mismo token; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(ValueLINQArena left, ValueLINQArena right)
            => left.Equals(right);

        /// <summary>
        /// Determina si dos instancias de <see cref="ValueLINQArena"/> son diferentes.
        /// </summary>
        /// <param name="left">Primera arena a comparar.</param>
        /// <param name="right">Segunda arena a comparar.</param>
        /// <returns><see langword="true"/> si ambas instancias poseen tokens distintos; en caso contrario, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(ValueLINQArena left, ValueLINQArena right)
            => !left.Equals(right);
    }
}
