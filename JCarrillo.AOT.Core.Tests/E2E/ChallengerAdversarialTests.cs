using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Excepciones;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;
using JCarrillo.AOT.Core.Extensiones.SemaphoreSlim;
using JCarrillo.AOT.Core.Extensiones.Boxing;

namespace JCarrillo.AOT.Core.Tests.E2E
{
    public class ChallengerAdversarialTests
    {
        private struct IntEqualsPredicate : IWhereDelegado<int, int>
        {
            public readonly bool Ejecutar(int item, int otro) => item == otro;
        }

        private static int ObtenerSlotsActivos<TItem>()
            => ValueLINQConfig.TamañoTabla - ValueLINQStateManager<TItem>.SlotsLibres;

        [Fact]
        public void ValueLINQFugaRecursosCuandoCapacidadAgotadaEnWhere()
        {
            // 1. Crear una consulta válida antes de agotar la capacidad
            int[] array = [1, 2, 3];
            ValueLINQStruct<int> queryOrigen = array.ToValueQuery();

            // 2. Agotar la capacidad del StateManager (4096 slots)
            List<ValueLINQStruct<int>> relleno = [];
            try
            {
                while (true)
                {
                    relleno.Add(new ValueLINQStruct<int>(1));
                }
            }
            catch (InvalidOperationException)
            {
                // Capacidad máxima alcanzada, correcto
            }

            // 3. Intentar realizar Where, lo cual fallará al intentar asignar el destino
            Action act = () => queryOrigen.Where(2, new IntEqualsPredicate());
            _ = act.Should().Throw<InvalidOperationException>();

            // 4. Aserción: La consulta origen fue liberada (no hay fuga de recursos)
            _ = queryOrigen.IsValido.Should().BeFalse("el try-finally debe haber liberado la consulta origen incluso cuando falló la asignación del destino");

            // Limpieza
            foreach (ValueLINQStruct<int> q in relleno)
            {
                q.Dispose();
            }
        }

        private static readonly int[] origen = [1, 2, 3];
        private static readonly int[] array456 = [4, 5, 6];

        [Fact]
        public void ValueLINQFugaRecursosCuandoConcatConTokenExpirado()
        {
            // 1. Crear una consulta origen válida
            ValueLINQStruct<int> query1 = origen.ToValueQuery();

            // 2. Crear otra consulta y desecharla para que expire
            ValueLINQStruct<int> query2 = array456.ToValueQuery();
            query2.Dispose();

            // 3. Llamar a Concat, lo cual lanzará una excepción al validar el token de query2
            Action act = () => query1.Concat(query2);
            _ = act.Should().Throw<ValueLinqSesionExpiradaException>();

            // 4. Aserción: query1 fue liberada (no hay fuga de recursos)
            _ = query1.IsValido.Should().BeFalse("query1 debe haber sido liberada en el bloque finally, ya que la excepción se maneja dentro del bloque try");
        }

        [Fact]
        public void ChunkFallaParcialNoProvocaFugaDeColeccionesCreadas()
        {
            // Registrar los slots activos iniciales
            int slotsIniciales = ObtenerSlotsActivos<int>();

            // Llenar el state manager hasta dejar solo 2 slots libres
            ValueLINQStruct<int>[] queriesDeRelleno = new ValueLINQStruct<int>[4096 - slotsIniciales - 2];
            try
            {
                for (int i = 0; i < queriesDeRelleno.Length; i++)
                {
                    queriesDeRelleno[i] = new ValueLINQStruct<int>(1);
                }

                // Ahora quedan exactamente 2 slots libres.
                // Creamos una consulta origen con 3 elementos y tamaño de chunk 1.
                // Esto requerirá crear 3 chunks independientes (3 slots).
                int[] origenDatos = [1, 2, 3];
                ValueLINQStruct<int> queryOrigen = origenDatos.ToValueQuery(); // Toma 1 slot. Queda 1 slot libre.

                // Intentar hacer Chunk. El primer chunk se creará con éxito (usando el último slot libre).
                // El segundo chunk intentará obtener un slot y lanzará InvalidOperationException por capacidad agotada.
                Action accionChunk = () => queryOrigen.Chunk(1);

                _ = accionChunk.Should().Throw<InvalidOperationException>()
                    .WithMessage("*Capacidad*máxima*");

                // Verificar que no hay fuga: todos los slots activos deben ser devueltos.
                int slotsActivosFinales = ObtenerSlotsActivos<int>();

                // Si no hay fuga, los slots activos deben ser exactamente 4096 - 2 = 4094 (el relleno)
                _ = slotsActivosFinales.Should().Be(4096 - 2, "no debe haber fuga de slots en caso de falla parcial en Chunk");
            }
            finally
            {
                // Limpiar
                for (int i = 0; i < queriesDeRelleno.Length; i++)
                {
                    queriesDeRelleno[i].Dispose();
                }
            }
        }

#if NET9_0_OR_GREATER
        [Fact]
        public void ValueLINQDelayUsoDeMemoriaLiberadaCuandoSeDesechaMediaEnumeracion()
        {
            // 1. Crear consulta origen
            ValueLINQStruct<int> query1 = origen.ToValueQuery();
            JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQDelayStruct<int, JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQSessionEnumerator<int>> pipeline = query1.Delay();
            JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQSessionEnumerator<int> enumerator = pipeline.GetEnumerator();

            // 2. Iniciar la enumeración (inicializa el enumerador y obtiene el Span)
            _ = enumerator.MoveNext().Should().BeTrue();
            _ = enumerator.Current.Should().Be(1);

            // 3. Desechar la consulta original a mitad de la enumeración
            query1.Dispose();

            // 4. Continuar la enumeración - debe retornar false de forma segura
            _ = enumerator.MoveNext().Should().BeFalse("debe retornar false en lugar de lanzar una excepción al haber sido desechada la sesión");
        }

        [Fact]
        public void ValueLINQDelayMoveNextDespuesDeDisponerRetornaFalseSinLanzarExcepcion()
        {
            // 1. Crear consulta origen y enumerador
            using ValueLINQStruct<int> query1 = origen.ToValueQuery();
            JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQDelayStruct<int, JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQSessionEnumerator<int>> pipeline = query1.Delay();
            JCarrillo.AOT.Core.ValueLINQ.Delay.ValueLINQSessionEnumerator<int> enumerator = pipeline.GetEnumerator();

            // 2. Disponer el enumerador de forma explícita
            enumerator.Dispose();

            // 3. Llamar a MoveNext() - debe retornar false de forma segura sin lanzar excepción
            _ = enumerator.MoveNext().Should().BeFalse("el enumerador dispuesto debe retornar false en subsiguientes MoveNext()");
        }
#endif

        [Fact]
        public unsafe void BoxingExtensionsFalsoPositivoConAsignacionEnHeapNativo()
        {
            using SemaphoreSlim semaphore = new(1, 1);
            semaphore.Wait(); // Adquirir el semáforo para evitar SemaphoreFullException en el bloque finally de Dispose()

            // 1. Asignar memoria en el heap nativo para la estructura
            int size = Unsafe.SizeOf<SemaphoreLock>();
            void* mem = NativeMemory.Alloc((nuint)size);
            try
            {
                // 2. Inicializar la estructura en la memoria nativa
                Unsafe.Write(mem, new SemaphoreLock(semaphore));
                IntPtr memPtr = (IntPtr)mem;

                // 3. Validar que no está boxeada (fallará porque la dirección está fuera de la pila del hilo)
                Action act = () =>
                {
                    ref SemaphoreLock lockRef = ref Unsafe.AsRef<SemaphoreLock>((void*)memPtr);
                    lockRef.Dispose();
                };
                _ = act.Should().Throw<InvalidOperationException>()
                   .WithMessage("*Se ha detectado boxing o ubicación en el Heap*");
            }
            finally
            {
                NativeMemory.Free(mem);
            }
        }

        [Fact]
        public void ValidarNoBoxeadoEnPilaProfundaConHeuristicaFallbackNoLanzaFalsoPositivo()
        {
            // Forzar el uso de la heurística de fallback modificando los límites mediante reflexión
            Type tipo = typeof(BoxingExtensions);
            FieldInfo? campoLow = tipo.GetField("_stackLow", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo? campoHigh = tipo.GetField("_stackHigh", BindingFlags.Static | BindingFlags.NonPublic);

            if (campoLow == null || campoHigh == null) return;

            // Simulamos que la heurística de fallback se ejecutó en un punto alto de la pila
            byte variableDePilaReferencia = 0;
            try
            {
                unsafe
                {
                    nuint direccionReferencia = (nuint)Unsafe.AsPointer(ref variableDePilaReferencia);

                    // Establecemos un límite inferior muy ajustado (por ejemplo, solo 4KB hacia abajo)
                    campoLow.SetValue(null, direccionReferencia - 4096);
                    campoHigh.SetValue(null, direccionReferencia + (8 * 1024 * 1024));
                }

                // Ahora ejecutamos un método recursivo profundo para simular una llamada que consume espacio de pila
                // y crea un struct legítimo en el stack que supera los 4KB de distancia.
                EjecutarEnPilaProfunda(0, 150); // 150 niveles de profundidad
            }
            finally
            {
                campoLow.SetValue(null, (nuint)0);
                campoHigh.SetValue(null, (nuint)0);
            }
        }

        private static void EjecutarEnPilaProfunda(int profundidadActual, int profundidadMaxima)
        {
            // Forzar asignación de pila en cada nivel de recursión para mover el SP
            int variableLocal1 = profundidadActual;
            int variableLocal2 = profundidadActual * 2;

            if (profundidadActual < profundidadMaxima)
            {
                EjecutarEnPilaProfunda(profundidadActual + 1, profundidadMaxima);
                // Evitar optimización de cola (tail call optimization)
                _ = variableLocal1.Should().Be(profundidadActual);
            }
            else
            {
                // Estamos en el punto más profundo de la pila.
                // Creamos un struct SemaphoreLock legítimo en la pila.
                using SemaphoreSlim semaforo = new(1, 1);
                SemaphoreLock bloqueo = new(semaforo);

                // Llamar a ValidarNoBoxeado. 
                // Dado que estamos a más de 4KB de distancia de la dirección de referencia inicial,
                // su dirección de memoria será menor que '_stackLow'.
                // Pero gracias a la verificación dinámica, no debe lanzar excepción.
                unsafe
                {
                    nint ptr = (nint)Unsafe.AsPointer(ref bloqueo);
                    Action accionValidar = () => Unsafe.AsRef<SemaphoreLock>((void*)ptr).Dispose();
                    _ = accionValidar.Should().NotThrow<InvalidOperationException>();
                }
            }
        }

        [Fact]
        public async Task AñadirConcurrenteEsSeguroYConservaTodosLosElementos()
        {
            // Creamos una única consulta compartida con capacidad inicial pequeña
            using ValueLINQStruct<int> query = new(10);

            const int numeroHilos = 10;
            const int elementosPorHilo = 100;
            Task[] tareas = new Task[numeroHilos];

            for (int i = 0; i < numeroHilos; i++)
            {
                int idHilo = i;
                tareas[i] = Task.Run(() =>
                {
                    for (int j = 0; j < elementosPorHilo; j++)
                    {
                        query.Añadir((idHilo * 1000) + j);
                    }
                });
            }

            // Esperar que terminen todas las tareas.
            await Task.WhenAll(tareas);

            // Aserción: El tamaño final debe ser exactamente el total de elementos añadidos (1000)
            int tamañoActual = ValueLINQStateManager<int>.ObtenerMetadatos(query.Token).TamañoActual;
            _ = tamañoActual.Should().Be(numeroHilos * elementosPorHilo,
                "los accesos concurrentes a Añadir deben ser seguros y no perder ningún elemento");
        }
    }
}
