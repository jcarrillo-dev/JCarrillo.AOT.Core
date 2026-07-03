using System.Runtime.CompilerServices;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    /// <summary>
    /// Represents a high-performance, stack-only ref struct wrapper around pooled resources for value-based LINQ operations.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the struct.</typeparam>
    public readonly ref struct ValueLINQRefStruct<T>
    {
        #region Token

        internal readonly long Token
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        #endregion

        #region EsValido

        /// <summary>
        /// Gets a value indicating whether the current instance is valid and has not been disposed.
        /// </summary>
        public readonly bool IsValido
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ValueLINQStateManager<T>.IsMetadatoValido(Token);
        }

        #endregion

        #region Constructores

        /// <summary>
        /// Initializes a new instance of the <see cref="ValueLINQRefStruct{T}"/> struct.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQRefStruct()
            => Token = 0L;

        // Creacion de los resultados, donde solo sabemos el resultado final (Solo deberia usarse este constructor)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQRefStruct(int tamañoMinimo)
            => Token = TokenHelper.LeerToken(ref ValueLINQStateManager<T>.ObtenerMetadatos(tamañoMinimo).Token);

        // Clonacion que apunta al mismo array (Nunca deberia usarse, solo esta para pruebas internas)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQRefStruct(long token)
            => Token = token;

        #endregion

        #region Añadir

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void Añadir(T valor) => ValueLINQStateManager<T>.Añadir(Token, valor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void Añadir(ReadOnlySpan<T> span) => ValueLINQStateManager<T>.Añadir(Token, span);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(ValueLINQRefStruct<T> valueLINQRefStruct)
            => Añadir(valueLINQRefStruct.Token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(ValueLINQStruct<T> valueLINQStruct)
            => Añadir(valueLINQStruct.Token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly void Añadir(long token) => ValueLINQStateManager<T>.Añadir(Token, token);

        #endregion

        #region Liberar

        /// <summary>
        /// Releases the pooled resources associated with this instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Dispose()
            => ValueLINQStateManager<T>.LiberarMetadatos(Token);

        #endregion

        #region Enumerator

        /// <summary>
        /// Returns an enumerator that iterates through the elements of the <see cref="ValueLINQRefStruct{T}"/>.
        /// </summary>
        /// <returns>A <see cref="Span{T}.Enumerator"/> for the elements.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Span<T>.Enumerator GetEnumerator()
        {
            if (Token == 0L)
                return Span<T>.Empty.GetEnumerator();

            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(Token);
            return metadatos.Array.AsSpan(0, metadatos.TamañoActual).GetEnumerator();
        }

        #endregion
    }
}
