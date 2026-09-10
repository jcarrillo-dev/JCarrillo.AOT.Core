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

*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.Extensiones.SemaphoreSlimBenchmarks`.

### Tabla 3: SemaphoreSlim vs SemaphoreLock (Medidos Multi-Runtime)
| Runtime / Engine | Método de Prueba | Tipo de Ejecución | Latencia (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `SemaphoreSlimSincrono` (Baseline) | Síncrono | 27.95 ns | **0 B** | 1.00 | Adquisición síncrona nativa BCL. |
| **NativeAOT 10.0** | `SemaphoreLockSincrono` | Síncrono | 30.96 ns | **0 B** | 1.11 | Overhead mínimo (+11%) con using stack struct. |
| **NativeAOT 10.0** | `SemaphoreSlimAsincrono` | Asíncrono | 30.56 ns | **0 B** | 1.09 | Adquisición asíncrona estándar en AOT. |
| **NativeAOT 10.0** | `SemaphoreLockAsincrono` | Asíncrono | 54.11 ns | **0 B** | 1.94 | ValueTask pooling y envoltura segura. |
| **.NET 10.0 JIT** | `SemaphoreSlimSincrono` (Baseline) | Síncrono | 26.70 ns | **0 B** | 1.00 | Adquisición síncrona BCL en RyuJIT 10. |
| **.NET 10.0 JIT** | `SemaphoreLockSincrono` | Síncrono | **26.46 ns** | **0 B** | **0.99** | Inlining completo: paridad absoluta con BCL. |
| **.NET 10.0 JIT** | `SemaphoreSlimAsincrono` | Asíncrono | 24.14 ns | **0 B** | 0.90 | Fast-path asíncrono BCL. |
| **.NET 10.0 JIT** | `SemaphoreLockAsincrono` | Asíncrono | 35.37 ns | **0 B** | 1.32 | Sobrecarga acotada (+32%) sin alocaciones. |
| **.NET 9.0 JIT** | `SemaphoreSlimSincrono` (Baseline) | Síncrono | 24.24 ns | **0 B** | 1.00 | Adquisición síncrona BCL en .NET 9. |
| **.NET 9.0 JIT** | `SemaphoreLockSincrono` | Síncrono | 28.81 ns | **0 B** | 1.19 | +19% de latencia con encapsulación segura. |
| **.NET 9.0 JIT** | `SemaphoreSlimAsincrono` | Asíncrono | 29.38 ns | **0 B** | 1.21 | Asíncrono en .NET 9. |
| **.NET 9.0 JIT** | `SemaphoreLockAsincrono` | Asíncrono | 41.79 ns | **0 B** | 1.73 | Fast-path ValueTask. |
| **.NET 8.0 JIT** | `SemaphoreSlimSincrono` (Baseline) | Síncrono | 23.94 ns | **0 B** | 1.00 | Adquisición síncrona BCL en .NET 8 LTS. |
| **.NET 8.0 JIT** | `SemaphoreLockSincrono` | Síncrono | 27.19 ns | **0 B** | 1.14 | +14% de latencia. |
| **.NET 8.0 JIT** | `SemaphoreSlimAsincrono` | Asíncrono | 30.88 ns | **0 B** | 1.29 | Asíncrono en .NET 8. |
| **.NET 8.0 JIT** | `SemaphoreLockAsincrono` | Asíncrono | 51.94 ns | **0 B** | 2.17 | Asíncrono con envoltura struct. |

---

## 3. Limitaciones y Trade-offs Técnicos (Ingeniería Honesta)

*   **Coste de Envoltura**: `SemaphoreLock` introduce entre un **0% y un 19% (medido)** de variación en llamadas síncronas en comparación con el uso directo de `SemaphoreSlim`, alcanzando paridad total (ratio 0.99x) bajo .NET 10.0 JIT gracias al inlining de métodos de extensión.
*   **Garantía Cero Asignaciones**: En todas las variantes evaluadas (.NET 8, 9, 10 y NativeAOT), tanto las llamadas síncronas como asíncronas registraron estrictamente **0 B (medido)** en el Heap de GC.
*   **Justificación de Diseño**: La pequeña variación de nanosegundos en rutas asíncronas representa el trade-off necesario a cambio de obtener validación en tiempo de ejecución en el stack, robustez sintáctica con el bloque `using` y prevención de fugas de semáforos por excepciones inadvertidas.

---

## 4. Optimización de Ruta Rápida (Fast-Path)

Para mitigar este coste y evitar allocations innecesarias en hilos concurrentes, las extensiones implementan un modelo dual:

1.  **Ruta Rápida (Fast-Path)**: Al invocar `EsperarAsync`, el método intenta adquirir el bloqueo inmediatamente mediante `Wait(0)`. Si el semáforo está libre, se retorna un `ValueTask<SemaphoreLock>` con la estructura ya resuelta, eludiendo la asignación de la máquina de estados asíncrona del compilador.
2.  **Ruta Lenta (Slow-Path)**: Si el semáforo está ocupado, la ejecución continúa de forma asíncrona mediante `EsperarAsyncSlow`, decorada con `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]` para que el runtime de .NET 10.0 recicle las máquinas de estado asíncronas de un pool y mantenga el perfil **zero-allocations**.
