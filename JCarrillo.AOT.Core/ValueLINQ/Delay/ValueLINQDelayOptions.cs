#if NET9_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ.Arena;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;

namespace JCarrillo.AOT.Core.ValueLINQ.Delay
{
    /// <summary>
    /// Estado por consulta que acompaña a una canalización perezosa a lo largo de toda la cadena de operadores.
    /// </summary>
    /// <remarks>
    /// El portador es no genérico para que los operadores puedan copiarlo tal cual entre canalizaciones con
    /// argumentos de tipo distintos, y para que ampliar el estado solo obligue a tocar este tipo.
    /// </remarks>
    internal readonly struct ValueLINQDelayOptions
    {
        /// <summary>
        /// Token de la arena propietaria de las sesiones que cree la canalización, o <c>0L</c> para la arena ambiente.
        /// </summary>
        /// <remarks>
        /// Se guarda el token completo y no solo el identificador porque los identificadores de arena se reciclan:
        /// al liberarse vuelven a la lista libre y se reparten de nuevo con la generación incrementada, de modo que
        /// solo el token entero distingue encarnaciones.
        /// </remarks>
        internal readonly long TokenArena;

        /// <summary>
        /// Renuncias explícitas activadas sobre la canalización.
        /// </summary>
        internal readonly ValueLINQDelayFlags Flags;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueLINQDelayOptions(long tokenArena, ValueLINQDelayFlags flags)
        {
            TokenArena = tokenArena;
            Flags = flags;
        }

        /// <summary>
        /// Obtiene las opciones de una canalización que vive en la arena ambiente.
        /// </summary>
        internal static ValueLINQDelayOptions Ambiente
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
        }

        /// <summary>
        /// Obtiene un valor que indica si la canalización está adscrita a una arena explícita.
        /// </summary>
        internal readonly bool HasArena
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TokenArena != 0L;
        }

        /// <summary>
        /// Obtiene el identificador de la arena propietaria, o cero si la canalización vive en la arena ambiente.
        /// </summary>
        internal readonly int IdArena
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TokenHelper.ObtenerIdTokenArena(TokenArena);
        }

        /// <summary>
        /// Adscribe la canalización a la arena indicada.
        /// </summary>
        /// <param name="arena">La arena propietaria de las sesiones que cree la canalización.</param>
        /// <returns>Unas opciones adscritas a <paramref name="arena"/>.</returns>
        /// <exception cref="ValueLinqArenaCruzadaException">
        /// Se lanza cuando la canalización ya pertenece a otra arena. Una canalización solo puede adquirir arena desde
        /// el estado ambiente: reasignarla mezclaría datos de ámbitos con vidas distintas y el buffer reservado en una
        /// podría liberarse mientras la canalización sigue leyéndolo.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly ValueLINQDelayOptions DesdeArena(ValueLINQArena arena)
        {
            if (HasArena)
                ValueLINQArenaManager.ThrowArenaCruzada(IdArena, arena.Id);

            return new ValueLINQDelayOptions(arena.TokenArena, Flags);
        }

        /// <summary>
        /// Hereda la arena de una sesión del motor eager a partir de su token.
        /// </summary>
        /// <param name="tokenSesion">El token de la sesión de origen.</param>
        /// <returns>Unas opciones adscritas a la arena de la sesión, o ambientes si la sesión vive en la arena ambiente.</returns>
        /// <exception cref="ValueLinqArenaCruzadaException">Se lanza cuando la canalización ya pertenece a otra arena.</exception>
        /// <exception cref="ValueLinqArenaInactivaException">Se lanza cuando la arena de la sesión ya fue liberada.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly ValueLINQDelayOptions DesdeSesion(long tokenSesion)
        {
            long tokenArena = ValueLINQArenaManager.TokenArenaDesdeSesion(tokenSesion);

            if (tokenArena == 0L)
                return this;

            if (HasArena)
                ValueLINQArenaManager.ThrowArenaCruzada(IdArena, TokenHelper.ObtenerIdTokenArena(tokenArena));

            return new ValueLINQDelayOptions(tokenArena, Flags);
        }

        /// <summary>
        /// Verifica que entre esta canalización y las que se combinan con ella intervenga una sola arena, además de la ambiente.
        /// </summary>
        /// <param name="segunda">Las opciones de la canalización que se combina.</param>
        /// <remarks>
        /// La arena ambiente no impone ámbito y convive con cualquier otra, pero dos arenas explícitas distintas sí son
        /// una violación: la vida de la consulta pasaría a ser la intersección de varias y la muerte parcial de una no
        /// tendría recuperación posible. La validación cubre todos los operandos, no solo el receptor, porque de lo
        /// contrario una canalización ambiente admitiría argumentos de arenas distintas entre sí. Quien decide dónde
        /// almacena el flujo sigue siendo el receptor.
        /// </remarks>
        /// <exception cref="ValueLinqArenaCruzadaException">Se lanza cuando intervienen dos arenas explícitas distintas.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void ValidarArenaUnica(ValueLINQDelayOptions segunda)
        {
            if (HasSinComprobarLimitesDeArena)
                return;

            _ = Combinar(IdArena, segunda);
        }

        /// <inheritdoc cref="ValidarArenaUnica(ValueLINQDelayOptions)"/>
        /// <param name="segunda">Las opciones de la segunda canalización.</param>
        /// <param name="tercera">Las opciones de la tercera canalización.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void ValidarArenaUnica(ValueLINQDelayOptions segunda, ValueLINQDelayOptions tercera)
        {
            if (HasSinComprobarLimitesDeArena)
                return;

            _ = Combinar(Combinar(IdArena, segunda), tercera);
        }

        /// <inheritdoc cref="ValidarArenaUnica(ValueLINQDelayOptions)"/>
        /// <param name="segunda">Las opciones de la segunda canalización.</param>
        /// <param name="tercera">Las opciones de la tercera canalización.</param>
        /// <param name="cuarta">Las opciones de la cuarta canalización.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void ValidarArenaUnica(ValueLINQDelayOptions segunda, ValueLINQDelayOptions tercera, ValueLINQDelayOptions cuarta)
        {
            if (HasSinComprobarLimitesDeArena)
                return;

            _ = Combinar(Combinar(Combinar(IdArena, segunda), tercera), cuarta);
        }

        /// <summary>
        /// Obtiene un valor que indica si la canalización renunció al recuento de arenas explícitas.
        /// </summary>
        internal readonly bool HasSinComprobarLimitesDeArena
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Flags & ValueLINQDelayFlags.SinComprobarLimitesDeArena) != 0;
        }

        /// <summary>
        /// Devuelve unas opciones que renuncian al recuento de arenas explícitas.
        /// </summary>
        /// <remarks>
        /// Es idempotente: aplicarlo sobre unas opciones que ya renunciaron no es un error. No altera la arena de almacenamiento ni ninguna otra comprobación.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly ValueLINQDelayOptions SinComprobarLimitesDeArena()
            => new(TokenArena, Flags | ValueLINQDelayFlags.SinComprobarLimitesDeArena);

        /// <summary>
        /// Acumula la arena de otra canalización sobre la de los operandos ya recorridos, delegando la regla en <see cref="ValueLINQArenaManager.CombinarArena"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Combinar(int arenaAcumulada, ValueLINQDelayOptions otras)
            => ValueLINQArenaManager.CombinarArena(arenaAcumulada, otras.IdArena);
    }
}
#endif
