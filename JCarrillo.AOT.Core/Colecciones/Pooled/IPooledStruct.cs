namespace JCarrillo.AOT.Core.Colecciones.Pooled
{
    /// <summary>
    /// Define la interfaz base para estructuras (structs) de colecciones que administran recursos alquilados
    /// a partir de un <see cref="System.Buffers.ArrayPool{T}"/>.
    /// </summary>
    /// <typeparam name="TItem">El tipo de los elementos contenidos en la estructura.</typeparam>
    public interface IPooledStruct<TItem> : IDisposable
    {
        /// <summary>
        /// Obtiene el número actual de elementos válidos y activos en la estructura.
        /// </summary>
        int Tamaño { get; }

        /// <summary>
        /// Obtiene un valor que indica si la estructura puede redimensionarse dinámicamente para ampliar su capacidad.
        /// </summary>
        bool EsAmpliable { get; }

        /// <summary>
        /// Obtiene un valor que indica si la estructura ha sido liberada y sus recursos subyacentes devueltos al pool.
        /// </summary>
        bool EstaDisposed { get; }

        /// <summary>
        /// Obtiene una referencia directa al elemento ubicado en el índice especificado.
        /// </summary>
        /// <param name="indice">El índice de base cero del elemento que se desea obtener por referencia.</param>
        /// <value>Una referencia directa (ref) al elemento en la posición solicitada, evitando copias de structs grandes en el stack.</value>
        /// <exception cref="IndexOutOfRangeException">
        /// Se lanza cuando el <paramref name="indice"/> está fuera de los límites válidos de la colección.
        /// Se utiliza esta excepción nativa para permitir la optimización del JIT eliminando validaciones redundantes de límites.
        /// </exception>
        ref TItem this[int indice] { get; }

        /// <summary>
        /// Obtiene una vista de acceso directo en memoria en forma de <see cref="Span{TItem}"/> sobre los elementos activos de la estructura.
        /// Permite operaciones de lectura y escritura seguras y ultrarrápidas sin asignaciones de memoria en el heap.
        /// </summary>
        Span<TItem> Span { get; }

        /// <summary>
        /// Obtiene una representación en memoria en forma de <see cref="Memory{TItem}"/> sobre los elementos activos de la estructura.
        /// Útil para operaciones asíncronas donde el ciclo de vida del buffer excede la duración del stack frame actual.
        /// </summary>
        Memory<TItem> Memory { get; }

        /// <summary>
        /// Retorna un enumerador de tipo struct sobre el buffer interno para permitir la iteración de la colección.
        /// Este método implementa el patrón de duck-typing compatible con el bucle foreach, evitando boxing en el heap
        /// y la asignación de un objeto enumerador tradicional.
        /// </summary>
        /// <returns>Un enumerador de tipo struct de alto rendimiento (<see cref="Span{TItem}.Enumerator"/>) sobre los elementos activos.</returns>
        Span<TItem>.Enumerator GetEnumerator();

        /// <summary>
        /// Intenta cambiar el tamaño de la estructura interna al tamaño especificado.
        /// </summary>
        /// <param name="nuevoTamaño">El nuevo tamaño sugerido para la estructura interna.</param>
        /// <returns>
        /// <see langword="true"/> si el tamaño fue modificado exitosamente; de lo contrario, <see langword="false"/>.
        /// Si la estructura no es ampliable o el nuevo tamaño no es válido, se retornará <see langword="false"/>.
        /// </returns>
        bool IntentarAmpliar(int nuevoTamaño);
    }
}
