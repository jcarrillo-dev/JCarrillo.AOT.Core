using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using System.Diagnostics;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.PruebasAdversariales;

public class PopperianReviewTests
{
    private struct ThrowingWhereDelegate : IWhereDelegado<int>
    {
        public bool Ejecutar(int parametro)
        {
            if (parametro == 5)
                throw new InvalidOperationException("Fallo inducido");
            return true;
        }
    }

    [Fact]
    public void ValueLINQStruct_ManejaExcepcionesEnOperadores_YLiberaArena()
    {
        // Vector: Excepción en iteración (Where) no debe filtrar memoria, ni corromper las arenas.

        int slotInical = ValueLINQStateManager<int>.SlotsLibres;

        try
        {
            using ValueLINQStruct<int> _ = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }.ToValueQuery()
                .Where(new ThrowingWhereDelegate());
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Debería recuperar los slots despues del finally
        int slotFinal = ValueLINQStateManager<int>.SlotsLibres;

        Assert.Equal(slotInical, slotFinal);
    }

    [Fact]
    public void PooledList_LanzaErrorAlBoxear()
    {
        // Vector: forzar el boxing de una colección y verificar que dispare error en dispose.
        PooledList<int> lista = new PooledList<int>();
        lista.Add(1);

        object boxeado = lista; // Boxing

        Assert.ThrowsAny<Exception>(() =>
        {
            ((IDisposable)boxeado).Dispose();
        });

        // Liberar el original para no hacer leak
        lista.Dispose();
    }
}

// Agregado manualmente en un nuevo archivo o aquí
