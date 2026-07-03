[Volver al Módulo de Extensiones](../README.md)

# Extensiones de Sincronización: SemaphoreSlim y Bloqueo en Pila


El módulo de sincronización en `JCarrillo.AOT.Core.Extensiones.SemaphoreSlim` (ver [Extensiones/SemaphoreSlim/](../../../JCarrillo.AOT.Core/Extensiones/SemaphoreSlim/)) proporciona mecanismos de exclusión mutua de ultra alto rendimiento diseñados específicamente para pipelines síncronos y asíncronos concurrentes.

---

## 1. Abstracción del Bloqueo: `SemaphoreLock`

En lugar de utilizar la sintaxis clásica de semáforos que requiere bloques `try-finally` manuales:
```csharp
await semaphore.WaitAsync();
try { /* Seccion critica */ }
finally { semaphore.Release(); }
```

El framework expone los métodos de extensión `Esperar` y `EsperarAsync` que retornan una estructura [SemaphoreLock.cs](../../../JCarrillo.AOT.Core/Extensiones/SemaphoreSlim/SemaphoreLock.cs) (un `ref struct` en pila) compatible con el patrón `using var` de C# 8+:
```csharp
using (var lockScope = await _semaphore.EsperarAsync())
{
    // Sección crítica segura y libre de allocations
}
```
Al finalizar el bloque, el método `Dispose()` del struct ejecuta automáticamente la llamada a `Release()` sobre el semáforo subyacente.

---

## 2. Métricas de Rendimiento (Medidas)

El benchmark evalúa la adquisición y liberación bajo exclusión mutua de forma síncrona y asíncrona sobre semáforos disponibles de forma inmediata.

*   **Entorno de Medición**: Windows 11, CPU AMD Ryzen 9 3950X, .NET SDK 10.0.301, runtime .NET 10.0.9 (medido).
*   **Harness**: BenchmarkDotNet v0.14.0, compilación en modo Release.

### Tabla 3: SemaphoreSlim vs SemaphoreLock (Medidos)
| Método de Prueba | Tipo de Ejecución | Latencia (Mean) | Heap Allocated | Ratio de Latencia |
| :--- | :--- | :---: | :---: | :---: |
| **SemaphoreSlim_Sincrono** (Baseline) | Síncrono | 535.9 ns | **0 B** | 1.00 |
| **SemaphoreLock_Sincrono** | Síncrono | 698.9 ns | **0 B** | 1.30 |
| | | | | |
| **SemaphoreSlim_Asincrono** (Baseline) | Asíncrono | 1,151.1 ns | **0 B** | 1.00 |
| **SemaphoreLock_Asincrono** | Asíncrono | 1,440.4 ns | **0 B** | 1.25 |

---

## 3. Limitaciones y Trade-offs Técnicos (Ingeniería Honesta)

*   **Coste de Envoltura**: `SemaphoreLock` introduce un coste adicional de CPU del **30.4% (medido)** en llamadas síncronas y del **25.1% (medido)** en llamadas asíncronas en comparación con el uso crudo de `SemaphoreSlim`.
*   **Justificación de Diseño**: Esta penalización en microsegundos representa el trade-off necesario a cambio de obtener validación en tiempo de ejecución en el stack, robustez sintáctica con el bloque `using` y soporte asíncrono sin generar allocations adicionales en el heap del GC (0 B de asignación).

---

## 4. Optimización de Ruta Rápida (Fast-Path)

Para mitigar este coste y evitar allocations innecesarias en hilos concurrentes, las extensiones implementan un modelo dual:

1.  **Ruta Rápida (Fast-Path)**: Al invocar `EsperarAsync`, el método intenta adquirir el bloqueo inmediatamente mediante `Wait(0)`. Si el semáforo está libre, se retorna un `ValueTask<SemaphoreLock>` con la estructura ya resuelta, eludiendo la asignación de la máquina de estados asíncrona del compilador.
2.  **Ruta Lenta (Slow-Path)**: Si el semáforo está ocupado, la ejecución continúa de forma asíncrona mediante `EsperarAsyncSlow`, decorada con `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` para que el runtime de .NET 10.0 recicle las máquinas de estado asíncronas de un pool y mantenga el perfil **zero-allocations**.
