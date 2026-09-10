#if NET9_0_OR_GREATER
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Delay;

namespace JCarrillo.AOT.Core.ValueLINQ.Marshalling
{
    /// <summary>
    /// Operaciones sobre canalizaciones perezosas que renuncian a alguna comprobación del motor y trasladan su garantía al código que las invoca.
    /// </summary>
    /// <remarks>
    /// El motor eager tiene su propia clase porque los dos motores no son simétricos: el eager copia sus operandos a una sesión destino antes de retornar, mientras que el perezoso retiene los enumeradores de origen durante toda la enumeración. Sus renuncias tampoco tienen por qué coincidir.
    /// </remarks>
    public static class ValueLINQDelayMarshal
    {
        /// <summary>
        /// Devuelve la canalización renunciando al recuento de arenas explícitas, de modo que pueda combinar operandos de arenas distintas.
        /// </summary>
        /// <typeparam name="T">El tipo de los elementos del flujo. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <typeparam name="TEnumerator">El tipo del enumerador de la canalización. Admite estructuras de referencia (allows ref struct).</typeparam>
        /// <param name="consulta">La canalización sobre la que se renuncia.</param>
        /// <returns>La misma canalización, con la renuncia activada a partir de este punto.</returns>
        /// <remarks>
        /// <para>
        /// <b>Qué se compromete a garantizar quien la usa:</b> que todas las arenas implicadas siguen vivas hasta que la enumeración termine o la consulta se materialice. Si alguna se libera antes, el enumerador que la lea lanzará; la promesa es verificable en ejecución y no un acto de fe.
        /// </para>
        /// <para>
        /// <b>Qué apaga:</b> únicamente el recuento de arenas explícitas entre los operandos de las operaciones que combinan canalizaciones. Nada más.
        /// </para>
        /// <para>
        /// <b>Qué sigue intacto:</b> los operadores que reservan siguen haciéndolo en la arena de la canalización y siguen comprobando que esa arena está viva; la enumeración sigue validando la sesión y lanza al perderla; y la arena de almacenamiento de la canalización sigue sin poder reasignarse, porque su estado nunca debe fragmentarse entre arenas.
        /// </para>
        /// <para>
        /// <b>Alcance:</b> la renuncia es una propiedad de la canalización, no de una llamada. No significa que este operador concreto no compruebe, sino que de aquí en adelante esta consulta no comprueba.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueLINQDelayStruct<T, TEnumerator> SinComprobarLimitesDeArena<T, TEnumerator>(this ValueLINQDelayStruct<T, TEnumerator> consulta)
            where T : allows ref struct
            where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
            => new(consulta._enumerator, consulta._opciones.SinComprobarLimitesDeArena());
    }
}
#endif
