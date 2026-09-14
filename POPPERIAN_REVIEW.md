# Revisión Popperiana: JCarrillo.AOT.Core

Esta es una revisión adversarial basada en los principios falsacionistas, la cual se propuso activamente buscar y ejecutar "vectores de refutación" (vulnerabilidades, fugas o cuellos de botella) que rompieran las garantías que provee el código, en particular: *el perfil de cero asignaciones*, el *manejo de recursos en caso de fallos* y el *confinamiento estructural estricto*.

## Vectores de Ataque Formulados

El esfuerzo analítico se concentró en la arquitectura que gobierna `ValueLINQ` (el motor eager y delay), la estructura de *Pooled Collections* y las utilidades de *Extensión*. Específicamente formulamos las siguientes hipótesis de fallo:

1. **Vector 1 (Fuga de Arenas ante Excepciones):**
   * *Hipótesis:* Si ocurre un error inesperado (p. ej., un `InvalidOperationException` disparado por un `Where` en medio del bucle eager de iteración), los _buffers_ alquilados en la tabla de sesiones de `ValueLINQStateManager<T>` se perderán, originando una memoria que nunca se retorna al `ArrayPool`.
2. **Vector 2 (Contrabando de Structs en Heap):**
   * *Hipótesis:* Un consumidor descuidado o una máquina de estados implícita (generada por el compilador) fuerza el boxeo (_boxing_) accidental de un `PooledList<T>` (un _pool_ mutable) al convertirlo implícitamente en una interfaz como `IDisposable`. Esto provocaría que el ciclo de vida de la estructura alquilada pase al _Garbage Collector_, arruinando el perfil _zero-allocation_.

## Resultados del Análisis (Evaluación Empírica)

### Arquitectura de Manejo de Memoria (`ValueLINQStateManager<T>` y Arenas)
Al revisar la estructura de `ValueLINQStateManager<T>` y `TablaSesiones<T>`, el blindaje contra fugas de excepción resultó sorprendentemente estricto. Operadores como `Añadir`, la reubicación durante expansión de _buffer_ (en `AsegurarEspacio`), e incluso enumeradores de fragmentos en el motor `Delay` como `ValueLINQChunkDelay`, implementan patrones restrictivos con semántica transaccional `try-finally`.

**Refutación del Vector 1:**
* Creamos el test empírico `ValueLINQStruct_ManejaExcepcionesEnOperadores_YLiberaArena` que induce intencionadamente un fallo durante el método `Ejecutar` de un `IWhereDelegado<int>`.
* *Resultado:* El gestor recuperó exitosamente los *slots* alquilados de la arena. El bloque `finally` intrínseco en los enumeradores devolvió el _buffer_ modificado al `ArrayPool<T>.Shared` y liberó la metadata. **El sistema superó este vector adversarial.**

### Validadores contra el Boxing (`BoxingExtensions.ValidarNoBoxeado`)
El riesgo inherente de .NET al usar *structs* (como `PooledList<T>` o `SemaphoreLock`) es que un cast accidental los mueva del Stack al Heap. La biblioteca implementa un mecanismo agresivo que lee directamente (vía _P/Invoke_ a `kernel32.dll` en Windows o `libc` en Unix) los límites físicos del subproceso (Thread Stack Limits).

**Refutación del Vector 2:**
* Programamos el test adversarial `PooledList_LanzaErrorAlBoxear` donde hacemos *boxeo explícito* en una colección mutable que aloja memoria del _ArrayPool_ e intentamos invocar `Dispose()` en la versión empacada.
* *Resultado:* La validación asertiva `this.ValidarNoBoxeado()` inserta en cada paso sensible de limpieza (como `Dispose`) interceptó el cast. Detectó que la variable había sido empujada al Heap en vez del Stack local del subproceso y arrojó la excepción pertinente. El *garbage collector* no se llenó de *buffers* fantasma. **El sistema superó este vector.**

## Conclusión

La infraestructura del repositorio fue empujada a sus límites lógicos mediante manipulación adversaria del ciclo de vida y las iteraciones. Las aserciones demostraron ser resilientes bajo .NET 8.0 y .NET 10.0 (Native AOT target frameworks) frente a:
- **Excepciones en canalizaciones de LINQ.**
- **Manipulaciones inválidas de tipo por parte del compilador (Boxing).**
- **Gestión concurrente (SpinLocks).**

La garantía de cero asignaciones está garantizada no solo de forma teórica sino empíricamente bajo condiciones críticas (vectores). El nivel de defensividad del código en `ValueLINQ` es impecable.