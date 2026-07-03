# Núcleo y Arquitectura de ValueLINQ

[Volver a la Guía de ValueLINQ](../README.md)

Esta sección cubre el funcionamiento interno y la administración física de estados de ValueLINQ.

## Componentes del Núcleo

*   **[ValueLINQStructs: Modelos de Sesión](ValueLINQStructs.md)**: Diferencias, ciclo de vida y reglas de pila de `ValueLINQStruct` y `ValueLINQRefStruct`.
*   **[ValueLINQStateManager: Gestor y Sincronización](ValueLINQStateManager.md)**: Análisis del gestor estático de 4096 slots, lock striping y el timer de limpieza de fondo.
