using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Enumerador para recorrer los elementos de una consulta de ValueLINQ de forma eficiente y sin asignaciones.
    /// </summary>
    /// <typeparam name="T">El tipo de los elementos enumerados.</typeparam>
    public ref struct ValueLINQEnumerator<T>
    {
        private readonly Span<T> _span;
        private int _index;

        /// <summary>
        /// Inicializa una nueva instancia de la estructura <see cref="ValueLINQEnumerator{T}"/> para el token de sesión especificado.
        /// </summary>
        /// <param name="token">El token de sesión de la consulta.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQEnumerator(long token)
        {
            if (token == 0L)
                _span = [];
            else
            {
                ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);
                _span = metadatos.Array.AsSpan(0, metadatos.TamañoActual);
            }
            _index = -1;
        }

        /// <summary>
        /// Obtiene una referencia al elemento en la posición actual del enumerador.
        /// </summary>
        public readonly ref T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _span[_index];
        }

        /// <summary>
        /// Desplaza el enumerador al siguiente elemento de la secuencia.
        /// </summary>
        /// <returns><see langword="true"/> si el enumerador se desplazó con éxito al siguiente elemento; <see langword="false"/> si el enumerador superó el final de la secuencia.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int next = _index + 1;
            bool hasNext = next < _span.Length;
            if (hasNext)
                _index = next;
            return hasNext;
        }
    }
}
