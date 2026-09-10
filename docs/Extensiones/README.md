# Módulo de Extensiones e Infraestructura

[Volver al Sitemap de Documentación](../README.md)

Este módulo agrupa extensiones de sincronización y utilidades de verificación en tiempo de ejecución.

## Módulos y Componentes

*   **[Semaphore Lock (Exclusión Mutua)](Semaphore/README.md)**: Primitivas asíncronas sobre `SemaphoreSlim` sin allocations.
*   **[Boxing Extensions (Detección en Stack)](Boxing/README.md)**: Validadores en tiempo de ejecución a nivel de punteros físicos contra el boxing de structs.
*   **Span Extensions**: Operaciones de propósito general sobre `Span<T>`. Actualmente `EliminarEnIndice`, que elimina un elemento desplazando los posteriores con `CopyTo` sobre el rango solapado; es la base compartida del `Remove` de `PooledList<T>` y `PooledListRef<T>`.
