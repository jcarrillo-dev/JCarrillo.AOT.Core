using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.Extensiones.Spans
{
    /// <summary>
    /// Métodos de extensión de propósito general sobre <see cref="Span{T}"/>.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Elimina el elemento situado en el índice indicado desplazando una posición a la izquierda todos los posteriores.
        /// </summary>
        /// <typeparam name="TItem">El tipo de los elementos del intervalo.</typeparam>
        /// <param name="span">El intervalo sobre el que se elimina.</param>
        /// <param name="indice">El índice del elemento a eliminar.</param>
        /// <remarks>
        /// El desplazamiento se hace con <see cref="Span{T}.CopyTo"/>, que admite intervalos solapados y mueve el bloque en lote en vez de elemento a elemento. La última posición queda lógicamente fuera de la colección: si los elementos contienen referencias se limpia para no retenerlas.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EliminarEnIndice<TItem>(this Span<TItem> span, int indice)
        {
            int siguiente = indice + 1;

            if (siguiente < span.Length)
                span[siguiente..].CopyTo(span[indice..]);

            if (RuntimeHelpers.IsReferenceOrContainsReferences<TItem>())
                span[^1] = default!;
        }
    }
}
