using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace JCarrillo.AOT.Core.ValueLINQ
{
    public record struct ValueLINQStruct<T> : IDisposable
    {
        #region Token

        private readonly long _token;

        internal readonly long Token
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _token;
        }

        #endregion

        #region EsValido

        public readonly bool IsValido
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ValueLINQStateManager<T>.IsMetadatoValido(_token);
        }

        #endregion

        #region Constructores

        // Construccion por defecto, esta clase se inicializara con token 0, lo cual no es valida para el manager
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueLINQStruct()
            => _token = 0L;

        // Creacion de los resultados, donde solo sabemos el resultado final (Solo deberia usarse este constructor)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQStruct(int tamañoMinimo)
            => _token = TokenHelper.LeerToken(ref ValueLINQStateManager<T>.ObtenerMetadatos(tamañoMinimo).Token);

        // Clonacion que apunta al mismo array (Nunca deberia usarse, solo esta para pruebas internas)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ValueLINQStruct(long token)
            => _token = token;

        #endregion

        #region Añadir

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(T valor)
        {
            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(_token);

            // Este metodo actualizara metadatos.Array para que pueda caber todos los datos, no es necesario obtenerlo de nuevo por que tenemos por puntero (ref) los metadatos del almacenamiento del manager
            ValueLINQStateManager<T>.AsegurarEspacio(_token, metadatos.TamañoActual + 1);

            metadatos.Array![metadatos.TamañoActual++] = valor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(ReadOnlySpan<T> span)
        {
            if (span.IsEmpty)
                return;

            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(_token);
            ValueLINQStateManager<T>.AsegurarEspacio(_token, metadatos.TamañoActual + span.Length);

            span.CopyTo(metadatos.Array.AsSpan(metadatos.TamañoActual));
            metadatos.TamañoActual += span.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(ValueLINQRefStruct<T> valueLINQRefStruct)
            => Añadir(valueLINQRefStruct.Token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Añadir(ValueLINQStruct<T> valueLINQStruct)
            => Añadir(valueLINQStruct._token);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Añadir(long token)
        {
            ref MetadatosSesion<T> metadatosOtro = ref ValueLINQStateManager<T>.ObtenerMetadatos(token);

            if (metadatosOtro.TamañoActual == 0)
                return;

            ref MetadatosSesion<T> metadatos = ref ValueLINQStateManager<T>.ObtenerMetadatos(_token);

            ValueLINQStateManager<T>.AsegurarEspacio(_token, metadatos.TamañoActual + metadatosOtro.TamañoActual);

            Span<T>
                actual = metadatos.Array.AsSpan(metadatos.TamañoActual),
                otro = metadatosOtro.Array.AsSpan(0, metadatosOtro.TamañoActual);

            // Ya se hizo el span con el offset, con lo cual no hace falta especificarlo
            otro.CopyTo(actual);
            metadatos.TamañoActual += metadatosOtro.TamañoActual;
        }

        #endregion

        #region Liberar

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
            => ValueLINQStateManager<T>.LiberarMetadatos(_token);

        #endregion

        #region Enumerator

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ValueLINQEnumerator<T> GetEnumerator()
            => new(Token);

        #endregion
    }
}
