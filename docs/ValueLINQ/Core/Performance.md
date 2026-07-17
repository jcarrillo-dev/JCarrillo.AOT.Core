# Auditoría de Rendimiento Core de ValueLINQ

## 1. Alcance y Categoría

Este documento está clasificado como una **Auditoría de Rendimiento Teórica de Análisis Estático y Arquitectónico** (estimado). Todas las métricas de rendimiento, sobrecargas de memoria y comportamientos de caché de hardware descritos aquí se derivan del análisis estático del diseño del código fuente y especificaciones teóricas de hardware (estimado). Representan proyecciones arquitectónicas en lugar de mediciones físicas en tiempo de ejecución, a menos que se indique explícitamente lo contrario (estimado).

---

## 2. Optimización de Localidad de Caché

El mecanismo de reciclaje de slots de sesión en `TablaSesiones<T>` (`Arena/TablaSesiones.cs:32-72`) utiliza una pila LIFO (Last-In, First-Out; Último en Entrar, Primero en Salir) (`_indicesLibresStack`) para gestionar los slots de buffer disponibles.

### 2.1 Beneficios en Caché de Hardware L1/L2/L3
En las arquitecturas de CPU modernas, cuando un hilo accede a una dirección de memoria, el bloque circundante (línea de caché) se carga en la caché L1 de la CPU (típicamente 32 KB a 64 KB por núcleo), la caché L2 (típicamente 512 KB a 1 MB por núcleo) y la caché L3 compartida (típicamente 16 MB a 64 MB). 
* **Localidad Temporal LIFO**: Al extraer (pop) índices de la parte superior de la pila (`--_topStack` en `Arena/TablaSesiones.cs:39`) y devolver (push) los índices a la parte superior (`_indicesLibresStack[_topStack++] = indice` en `Arena/TablaSesiones.cs:71`), el sistema reutiliza de inmediato el slot que se liberó más recientemente.
* **Retención en Caché**: Esta reutilización inmediata asegura que el struct de metadatos (`MetadatosSesion<T>`) y la referencia del array subyacente (`metadato.Array`) permanezcan activos dentro de la jerarquía de caché L1/L2 del núcleo de la CPU (estimado). Reutilizar un slot "caliente" evita la latencia de recuperar el struct desde la caché L3 más lenta o la memoria RAM del sistema.
* **Contraste con FIFO**: Por el contrario, una estrategia de reciclaje FIFO (First-In, First-Out) rotaría a través de los 4096 slots. Bajo una concurrencia baja a media, esto daría lugar a que un slot solo se reutilizara después de que se hayan asignado los otros 4095 slots. Para ese momento, los metadatos del slot habrían sido expulsados de las cachés L1/L2/L3, provocando fallos de caché (*cache misses*) y forzando el acceso a la RAM del sistema (estimado).

---

## 3. Prevención de Uso Compartido Falso (False Sharing)

El *false sharing* o uso compartido falso ocurre cuando dos hilos que se ejecutan en diferentes núcleos de CPU modifican variables que residen en la misma línea de caché. Incluso si las variables son independientes, el protocolo de coherencia de caché de la CPU (ej. MESI) invalida toda la línea de caché en todos los núcleos cuando ocurre una escritura. Esto obliga a los núcleos a recargar la línea de caché desde la caché L3 o la RAM, provocando una degradación del rendimiento.

### 3.1 Alineamiento a Línea de Caché de 64 Bytes
ValueLINQ previene el *false sharing* alineando las estructuras de bloqueo y metadatos a límites de 64 bytes (el tamaño de la línea de caché estándar en arquitecturas x86_64 y ARM64) (estimado).

* **Alineamiento de Bloqueo (Lock) (`SpinLockSlot`)**:
  El struct `SpinLockSlot` está decorado con disposición secuencial y un tamaño forzado de 64 bytes (`SpinLockSlot.cs:6-10`):
  ```csharp
  [StructLayout(LayoutKind.Sequential, Size = 64)]
  internal struct SpinLockSlot { public SpinLock Lock; }
  ```
  Como cada `SpinLockSlot` ocupa exactamente 64 bytes, se garantiza que cada spinlock en el array escalonado `_spinLocks` resida en una línea de caché distinta. Las adquisiciones concurrentes de bloqueos para el Slot A y el Slot B no provocan bucles de invalidación de líneas de caché (efecto ping-pong) entre los núcleos de la CPU (estimado).
* **Alineamiento de Metadatos (`MetadatosSesion<T>`)**:
  El struct `MetadatosSesion<T>` incluye tres campos de relleno (padding) de 64 bits (`long Relleno1`, `Relleno2`, `Relleno3`) para forzar su tamaño a 64 bytes (`MetadatosSesion.cs:3-14`). Esto evita que un hilo que escribe en `UltimoAcceso` o `Version` en el Slot A invalide la línea de caché de otro hilo que lee o escribe en el Slot B (estimado).

---

## 4. Análisis de Huella de Memoria

La decisión arquitectónica de pre-asignar las estructuras de gestión y materializar las estructuras de datos de forma perezosa introduce compromisos específicos de memoria estática y dinámica.

### 4.1 Sobrecarga Estática (Gestores de Estado Global)
La huella de memoria estática se asigna cuando el runtime carga las clases:
* **Estructuras del Gestor de Arenas**:
  * `EstadoArena[] _arenas` (`ValueLINQArenaManager.cs:11`): 4096 entradas * 32 bytes (tamaño del struct `EstadoArena`) = 131.072 bytes (128 KB) (estimado).
  * `int[] _idsLibres` (`ValueLINQArenaManager.cs:15`): 4095 entradas * 4 bytes = 16.380 bytes (~16 KB) (estimado).
* **Tabla de Referencias del Gestor de Estado**:
  * Array de referencias `_tablas` (`ValueLINQStateManager.cs:15`): 4096 entradas * 8 bytes (tamaño de la referencia en sistemas de 64 bits) = 32.768 bytes (32 KB) por cada tipo genérico `T` (estimado).

### 4.2 Sobrecarga Dinámica (TablaSesiones<T>)
Cuando se materializa una `TablaSesiones<T>` para una arena activa específica, la huella inicial incluye:
* Array `_indicesLibresStack` (`Arena/TablaSesiones.cs:23`): 4096 entradas * 4 bytes = 16.380 bytes (~16 KB) (estimado).
* Cabeceras de array escalonado: Array de referencias `_datos` (64 * 8 bytes = 512 bytes) y array de referencias `_spinLocks` (64 * 8 bytes = 512 bytes) (estimado).
* **Asignación Perezosa (Lazy) de Particiones**: Los sub-arrays (`_datos[particion]` y `_spinLocks[particion]`) se materializan solo cuando se accede a ellos (`Arena/TablaSesiones.cs:42-62`).
  * Cada partición materializada contiene 64 slots.
  * Array de datos de la partición: 64 entradas * 64 bytes (tamaño de `MetadatosSesion<T>`) = 4.096 bytes (4 KB) (estimado).
  * Array de bloqueos de la partición: 64 entradas * 64 bytes (tamaño de `SpinLockSlot`) = 4.096 bytes (4 KB) (estimado).
  * **Huella Total por Partición**: 8 KB por partición materializada (estimado).

### 4.3 Compromisos de Memoria por Relleno (Padding)
Para imponer la alineación de 64 bytes, el sistema intercambia capacidad de memoria por velocidad de ejecución:
* **Desperdicio por Relleno de Metadatos**: En `MetadatosSesion<T>`, 24 bytes de los 64 (37,5%) son campos de relleno (`Relleno1-3`) (estimado). A lo largo de una tabla totalmente materializada de 4096 slots, esto representa $4096 \times 24 \text{ bytes} = 98.304 \text{ bytes}$ (96 KB) de memoria no utilizada por tipo `T` (estimado).
* **Desperdicio por Relleno de Bloqueos (Lock)**: En `SpinLockSlot`, una estructura `SpinLock` (que requiere 4 bytes en runtimes estándar) se rellena hasta 64 bytes, desperdiciando 60 bytes (93,75%) (estimado). En los 4096 slots, esto representa $4096 \times 60 \text{ bytes} = 245.760 \text{ bytes}$ (240 KB) de memoria no utilizada por tipo `T` (estimado).
* **Conservación a través de Particiones LIFO**: La estrategia de pila LIFO mitiga este desperdicio. Bajo baja concurrencia, solo se materializa la primera partición, restringiendo la sobrecarga de relleno activa a $64 \times (24 \text{ bytes} + 60 \text{ bytes}) = 5.376 \text{ bytes}$ (~5,2 KB) (estimado). Las 63 particiones restantes permanecen sin materializar, ahorrando hasta $63 \times 8 \text{ KB} = 504 \text{ KB}$ de espacio en el heap en comparación con un modelo plano pre-asignado (estimado).

---

## 5. Sincronización y Contención

### 5.1 Lock Striping vs. Bloqueo Global
* **Bloqueo (Lock) Global del Gestor**: Las operaciones globales (alquilar/liberar arenas) están protegidas por un único spinlock en el gestor (`ValueLINQArenaManager.cs:13`). Esto introduce un cuello de botella de serialización, pero la sección crítica es breve (solo operaciones de índices y manipulaciones de bits), minimizando el tiempo de retención (estimado).
* **Segmentación de Bloqueos (Lock Striping) por Slot**: Dentro de `TablaSesiones<T>`, las operaciones sobre las sesiones se sincronizan usando spinlocks individuales para cada slot (`_spinLocks[particion][index].Lock`) (`Arena/TablaSesiones.cs:160`). Puesto que el bloqueo se restringe al slot específico al que se accede, hasta 4096 hilos concurrentes pueden interactuar con sesiones separadas del tipo `T` simultáneamente sin contención de bloqueos (estimado).

### 5.2 Asignación en Pila de ValueLINQSpinLock
El sistema utiliza el `ref struct` `ValueLINQSpinLock` (`ValueLINQSpinLock.cs:5-24`) para implementar el bloqueo basado en RAII.
* **Costo de Asignación**: Al ser un `ref struct`, se asigna en la pila (stack). El coste de asignación de memoria es de 0 bytes en el heap gestionado, evitando la sobrecarga del recolector de basura (GC) (estimado).
* **Contraste con Bloqueo Monitor (Monitor Lock)**: Un bloqueo monitor estándar basado en clases (`lock(obj)`) requiere asignar un objeto de sincronización en el heap (sobrecarga de 24 bytes en procesos de 64 bits) e incurre en sobrecarga en tiempo de ejecución para asociar el objeto con las primitivas de sincronización de hilos del sistema operativo.
* **Características de SpinLock**: `SpinLock` utiliza operaciones atómicas de la CPU (p. ej., `Interlocked.CompareExchange`) para adquirir el bloqueo. Para secciones críticas cortas, esto evita poner el hilo a dormir (sleep), previniendo la sobrecarga de los cambios de contexto (*context switches*) del sistema operativo (típicamente $1.5 \text{ a } 5 \text{ microsegundos}$) (estimado). Sin embargo, si el bloqueo se mantiene durante periodos prolongados, el ciclo de espera activa (spinning) consumirá ciclos de la CPU.

---

## 6. Límites del Análisis ("Lo que este análisis/benchmark NO mide")

Esta auditoría de rendimiento es una evaluación teórica y estática del diseño del código fuente. No mide:
1. **Métricas de Contadores de Hardware (PMU)**: Este análisis no mide las proporciones físicas de fallos (miss ratios) de caché L1/L2/L3, Instrucciones Por Ciclo (IPC), fallos del Translation Lookaside Buffer (TLB), ni las tasas de predicción incorrecta de ramificaciones (*branch misprediction*). Estas métricas requieren la ejecución con herramientas de perfilado de Unidades de Monitoreo de Rendimiento (PMU) de hardware.
2. **Contención del Programador de Hilos del SO (OS Scheduler)**: Este análisis no mide la sobrecarga real de latencia de los cambios de contexto de los hilos, el aparcamiento/desaparcamiento de hilos por parte del SO, ni la inanición de hilos bajo una sobresuscripción alta de la CPU (donde el número de hilos activos excede el número de núcleos físicos de CPU).
3. **Benchmarks Brutos de Latencia y Ejecución**: Este análisis no presenta tiempos de ejecución empíricos (tales como latencias medias, desviaciones estándar o márgenes de error en nanosegundos) bajo condiciones de carga variables. Las velocidades de ejecución en el mundo real dependen del hardware, el ancho de banda del bus de memoria y las optimizaciones específicas de compilación JIT aplicadas en tiempo de ejecución.
