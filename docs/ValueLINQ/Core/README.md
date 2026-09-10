# Núcleo y Arquitectura de ValueLINQ

[Volver a la Guía de ValueLINQ](../README.md)

Esta sección cubre el funcionamiento interno y la administración física de estados de ValueLINQ.

## Componentes del Núcleo

*   **[Arquitectura Física](Architecture.md)**: Diseño interno, memoria LIFO/FIFO y gestión de metadatos de sesión (con diagramas de flujo).
*   **[Rendimiento y Concurrencia (Performance)](Performance.md)**: Análisis avanzado de rendimiento, contención de `SpinLock` y seguridad en punteros con `Volatile`.
*   **[ValueLINQStructs: Modelos de Sesión](ValueLINQStructs.md)**: Diferencias, ciclo de vida y reglas de pila de `ValueLINQStruct` y `ValueLINQRefStruct`.
*   **[ValueLINQStateManager: Gestor y Sincronización](ValueLINQStateManager.md)**: Análisis del gestor con tablas de sesión particionadas por (tipo, arena) (64 particiones × 64 slots = 4096 slots por tabla, materializadas de forma perezosa), bloqueo por SpinLock por slot (sin lock striping de objetos) y el timer de limpieza de fondo.
*   **[Arenas de Memoria](Arenas.md)**: Ámbitos de memoria explícitos (`ValueLINQArena`), la arena ambiente y persistente (arena 0), propagación por los operadores eager y perezosos, restricción de una sola arena por consulta y cómo trasladar datos de una arena a otra, y contrato de vida útil.
    *   **[Verificación del sistema de arenas](Arenas.Verificacion.md)**: registro de ingeniería del anterior — qué está probado y con qué contramuestra, qué se midió y con qué entorno, y qué sigue sin cubrir.
