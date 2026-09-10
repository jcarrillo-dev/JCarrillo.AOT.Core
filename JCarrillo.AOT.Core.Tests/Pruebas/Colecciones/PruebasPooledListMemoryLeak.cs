#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;
using Xunit;

namespace JCarrillo.AOT.Core.Tests.Pruebas.Colecciones
{
    public class PruebasPooledListMemoryLeak
    {
        [Fact]
        public void PooledList_RemoveAt_LimpiaLaRanuraSobranteYPermiteRecoleccionGC()
        {
            // Preparar
            using PooledList<object> lista = new(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            // Actuar
            lista.RemoveAt(1);

            // Verificar: El tamaño se reduce a 2 y los elementos restantes son objA y objC
            _ = lista.Tamaño.Should().Be(2);
            _ = lista.Span.ToArray().Should().Equal(objA, objC);

            // Verificar la ranura interna en el índice 2 (_indiceInsercion) mediante reflexión
            FieldInfo? fieldInfo = typeof(PooledList<object>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            _ = fieldInfo.Should().NotBeNull();

            object[]? arrayInterno = fieldInfo!.GetValue(lista) as object[];
            _ = arrayInterno.Should().NotBeNull();
            _ = arrayInterno![2].Should().BeNull("la ranura sobrante en _indiceInsercion debe limpiarse a null (default!) para evitar fugas de memoria");
        }

        [Fact]
        public void PooledList_Remove_LimpiaLaRanuraSobranteYPermiteRecoleccionGC()
        {
            // Preparar
            using PooledList<object> lista = new(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            // Actuar
            bool eliminado = lista.Remove(objB);

            // Verificar
            _ = eliminado.Should().BeTrue();
            _ = lista.Tamaño.Should().Be(2);
            _ = lista.Span.ToArray().Should().Equal(objA, objC);

            // Verificar la ranura interna en el índice 2 (_indiceInsercion)
            FieldInfo? fieldInfo = typeof(PooledList<object>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
            object[]? arrayInterno = fieldInfo!.GetValue(lista) as object[];
            _ = arrayInterno![2].Should().BeNull("la ranura sobrante debe ser null");
        }

        [Fact]
        public void PooledListRef_RemoveAt_LimpiaLaRanuraSobrante()
        {
            // Preparar
            using PooledListRef<object> lista = new(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            // Actuar
            lista.RemoveAt(1);

            // Verificar
            _ = lista.Tamaño.Should().Be(2);
            _ = lista.Span.ToArray().Should().Equal(objA, objC);
        }

        [Fact]
        public void PooledListRef_Remove_LimpiaLaRanuraSobrante()
        {
            // Preparar
            using PooledListRef<object> lista = new(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            // Actuar
            bool eliminado = lista.Remove(objB);

            // Verificar
            _ = eliminado.Should().BeTrue();
            _ = lista.Tamaño.Should().Be(2);
            _ = lista.Span.ToArray().Should().Equal(objA, objC);
        }

        [Fact]
        public void PooledList_RemoveAt_ObjetoEliminadoEsRecolectadoPorElGC()
        {
            // Preparar & Actuar
            WeakReference weakRef = EjecutarRemoveAtYRetornarWeakRef(out PooledList<object> lista);

            // Forzar recolección de basura
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Verificar
            _ = weakRef.IsAlive.Should().BeFalse("el objeto eliminado no debe tener referencias retenidas por el buffer de PooledList");
            _ = lista.Tamaño.Should().Be(2);

            lista.Dispose();
        }

        [Fact]
        public void PooledList_Remove_ObjetoEliminadoEsRecolectadoPorElGC()
        {
            // Preparar & Actuar
            WeakReference weakRef = EjecutarRemoveYRetornarWeakRef(out PooledList<object> lista);

            // Forzar recolección de basura
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Verificar
            _ = weakRef.IsAlive.Should().BeFalse("el objeto eliminado no debe ser retenido tras Remove");
            _ = lista.Tamaño.Should().Be(2);

            lista.Dispose();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference EjecutarRemoveAtYRetornarWeakRef(out PooledList<object> lista)
        {
            lista = new PooledList<object>(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            lista.RemoveAt(1);

            return new WeakReference(objB);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference EjecutarRemoveYRetornarWeakRef(out PooledList<object> lista)
        {
            lista = new PooledList<object>(8);
            object objA = new();
            object objB = new();
            object objC = new();

            lista.Add(objA);
            lista.Add(objB);
            lista.Add(objC);

            lista.Remove(objB);

            return new WeakReference(objB);
        }
    }
}
