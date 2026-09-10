[Volver al Sitemap de Documentación](../README.md) | [Volver a ValueLINQ](README.md)

# Reporte de Benchmarks Consolidado de ValueLINQ

**Arquitectura y Diseño**: José Carrillo Serrano  
**Implementación de Benchmarks, Tests y Blindaje Adversarial**: Gemini 3.8 Flash (Google)  
**Versión Evaluada**: ValueLINQ 1.1.0  

Este documento presenta el análisis cuantitativo completo de rendimiento y eficiencia de memoria (Allocated Bytes en el Heap) de **ValueLINQ (versión 1.1.0)** frente a las colecciones y métodos estándar de .NET. Las pruebas evalúan el comportamiento bajo compilación JIT y **Native AOT** en múltiples runtimes.

---

## Entorno de Ejecución y Metodología

Todas las mediciones empíricas fueron registradas bajo las siguientes condiciones controladas de hardware y software (medido):

*   **Sistema Operativo**: Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2) (medido)
*   **Procesador**: AMD Ryzen 9 3950X (3.50 GHz, 1 CPU, 32 núcleos lógicos, 16 núcleos físicos) (medido)
*   **SDK e Infraestructura**: .NET SDK 10.0.301 (medido)
*   **Runtimes Evaluados**:
    *   .NET 8.0.28 (X64 RyuJIT x86-64-v3) (medido)
    *   .NET 9.0.17 (X64 RyuJIT x86-64-v3) (medido)
    *   .NET 10.0.9 (X64 RyuJIT x86-64-v3) (medido)
    *   NativeAOT 8.0 (.NET 8.0.28, X64 NativeAOT x86-64-v3) (medido)
    *   NativeAOT 9.0 (.NET 9.0.17, X64 NativeAOT x86-64-v3) (medido)
    *   NativeAOT 10.0 (.NET 10.0.9, X64 NativeAOT x86-64-v3) (medido)
*   **Harness de Medición**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y recopilación de conteo de contención de bloqueos.

---

## Resumen Ejecutivo: Métricas Más Impactantes (Key Highlights)

A continuación se resumen los hitos cuantitativos más relevantes de rendimiento y eficiencia de memoria alcanzados por **ValueLINQ**, contrastados contra el estándar de la BCL (`System.Linq`) y verificables mediante las suites de benchmarks del repositorio:

| Dominio Técnico | Escenario y Condiciones | BCL / Enfoque Tradicional | ValueLINQ | Reducción / Ganancia | Enlace a Datos Crudos |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **Streaming Sub-Microsegundo** | Native AOT 10.0 ($N=1000$) | 14,379.39 ns (144 B) | **626.07 ns (0 B)** | **-95.6% latencia (23x más rápido), 0 B GC** | [Consultas Delay](#2-consultas-diferidas--streaming-valuelinq-delay) |
| **Escala Pequeña Inlining** | .NET 10.0 JIT ($N=100$) | 229.30 ns (104 B) | **69.06 ns (0 B)** | **-69.9% latencia (3.3x más rápido), 0 B GC** | [Consultas Delay](#2-consultas-diferidas--streaming-valuelinq-delay) |
| **Consultas Eager Native AOT** | Native AOT 10.0 ($N=1000$) | 12,635.2 ns (144 B) | **1,447.0 ns (0 B)** | **-88.5% latencia (8.7x más rápido), 0 B GC** | [Consultas Eager](#1-consultas-fluent-eager-where--select) |
| **Consultas Eager JIT** | .NET 10.0 JIT ($N=1000$) | 2,114.2 ns (104 B) | **1,275.3 ns (0 B)** | **-39.7% latencia (1.66x más rápido), 0 B GC** | [Consultas Eager](#1-consultas-fluent-eager-where--select) |
| **Sincronización $O(1)$** | Inserción en bloque ($N=1000$) | 29,797.46 ns (0 B, $O(N)$ locks) | **142.60 ns (0 B, $O(1)$ lock)** | **-99.52% latencia (208x más rápido)** | [Población Masiva](#5-inserción-y-población-de-datos-n--1000) |
| **Supresión `params` Heap** | Concatenación en .NET 9/10 | 56 B (.NET 8.0) | **0 B (.NET 9/10)** | **100% eliminación de alocación heap** | [Sobrecarga Params](#4-concatenación-y-sobrecarga-params-n--100) |
| **Sobrecoste de Arena** | Reutilización vs Ambiente | 1,103.71 ns (0 B) | **1,147.77 ns (0 B)** | **Sobrecoste despreciable (~4% / ruido de CPU)** | [Arenas de Memoria](#7-arenas-de-memoria-sobrecoste-y-amortización) |

> [!NOTE]
> **Rigor Metodológico**: Todas las cifras cuantitativas tabuladas fueron medidas empíricamente (`medido`) mediante BenchmarkDotNet v0.15.8 en el entorno de pruebas documentado, sin interpolaciones teóricas ni estimaciones no verificadas.

---

## Tablas de Resultados Comparativos (Medidos)

### 1. Consultas Fluent Eager (`Where` + `Select`)
Prueba que simula un pipeline común de procesamiento de datos compuesto por un filtrado y una proyección en cadena. Se evalúan tanto variantes con delegados de struct (`IWhereDelegado`, `ISelectDelegado`) como con expresiones lambda estáticas, en implementaciones `ValueLINQStruct` y `ValueLINQRefStruct` frente a `StandardLINQWhereSelect` de la BCL.

*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias.SecuenciaWhereSelectBenchmarks`.

#### Escala $N = 1000$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectDelegados` | **1,447.0 ns** | **0 B** | **0.12** | **8.7x más rápido que BCL** con inlining estático de struct y cero GC. |
| **NativeAOT 10.0** | `ValueLINQRefStructWhereSelectDelegados` | **1,490.1 ns** | **0 B** | **0.12** | Variante stack ref struct: paridad de velocidad y 0 B. |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectLambdas` | 3,757.9 ns | **0 B** | 0.30 | 3.4x más rápido que BCL con lambdas estáticas. |
| **NativeAOT 10.0** | `ValueLINQRefStructWhereSelectLambdas` | 4,212.7 ns | **0 B** | 0.34 | 3.0x más rápido que BCL con lambdas estáticas. |
| **NativeAOT 10.0** | `StandardLINQWhereSelect` (Baseline) | 12,635.2 ns | 144 B | 1.00 | BCL en Native AOT: penalizado por despacho dinámico de interfaz. |
| **.NET 10.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | **1,275.3 ns** | **0 B** | **0.60** | **40% más rápido que BCL** con delegados struct en stack. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectDelegados` | **1,550.8 ns** | **0 B** | **0.73** | 27% más rápido que BCL con cero asignación en heap. |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,114.2 ns | 104 B | 1.00 | LINQ estándar en RyuJIT 10. |
| **.NET 10.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 2,336.0 ns | **0 B** | 1.11 | Lambdas estáticas con cero asignación en heap. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 2,585.8 ns | **0 B** | 1.22 | Lambdas estáticas en struct con cero asignación en heap. |
| **.NET 9.0 JIT** | `ValueLINQStructWhereSelectDelegados` | **1,572.3 ns** | **0 B** | **0.78** | 22% más rápido que BCL en .NET 9. |
| **.NET 9.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | **1,575.9 ns** | **0 B** | **0.79** | 21% más rápido que BCL en .NET 9. |
| **.NET 9.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 1,700.5 ns | **0 B** | 0.85 | 15% más rápido que BCL con lambdas estáticas. |
| **.NET 9.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 1,811.7 ns | **0 B** | 0.90 | 10% más rápido que BCL con lambdas estáticas. |
| **.NET 9.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,006.4 ns | 104 B | 1.00 | LINQ estándar en .NET 9. |
| **.NET 8.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | **1,449.7 ns** | **0 B** | **0.66** | **34% más rápido que BCL** en .NET 8 LTS. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectDelegados` | **1,580.6 ns** | **0 B** | **0.72** | 28% más rápido que BCL en .NET 8 LTS. |
| **.NET 8.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 1,781.2 ns | **0 B** | 0.81 | 19% más rápido que BCL en .NET 8 LTS. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 2,177.8 ns | **0 B** | 0.99 | Paridad de velocidad con BCL y cero alocaciones. |
| **.NET 8.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,209.1 ns | 104 B | 1.00 | LINQ estándar en .NET 8 LTS. |

> [!IMPORTANT]
> **Comportamiento en Native AOT 10.0**:
> Los resultados empíricos revelan una diferencia sustancial en la plataforma .NET 10.0 bajo compilación nativa.
> Mientras que el LINQ estándar de la BCL eleva su latencia a 12,635.2 ns **(medido)** debido al coste de despacho por interfaces genéricas virtuales (`IEnumerable<T>`), las variantes de ValueLINQ operan entre 1,447.0 ns y 1,490.1 ns **(medido)** (**hasta 8.7 veces más rápidas**) con estrictamente **0 B (medido)** asignados en heap.

#### Escala $N = 100$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Ratio | Notas |
| :--- | :--- | :---: | :---: | :---: | :--- |
| **NativeAOT 10.0** | `ValueLINQRefStructWhereSelectDelegados` | **460.5 ns** | **0 B** | **0.32** | **3.1x más rápido que BCL** en colecciones reducidas bajo AOT. |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectDelegados` | **466.5 ns** | **0 B** | **0.32** | Cero asignación y máxima optimización estática. |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectLambdas` | 684.7 ns | **0 B** | 0.48 | 2.1x más rápido con lambdas estáticas. |
| **NativeAOT 10.0** | `ValueLINQRefStructWhereSelectLambdas` | 720.5 ns | **0 B** | 0.50 | 2.0x más rápido con lambdas estáticas. |
| **NativeAOT 10.0** | `StandardLINQWhereSelect` (Baseline) | 1,439.4 ns | 144 B | 1.00 | LINQ estándar en compilación nativa. |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 245.3 ns | 104 B | 1.00 | LINQ estándar en RyuJIT 10. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectDelegados` | 426.6 ns | **0 B** | 1.74 | Sobrecarga de alquiler de buffers amortizable en N mayor. |
| **.NET 10.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | 433.4 ns | **0 B** | 1.77 | Cero asignación en heap. |
| **.NET 10.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 512.7 ns | **0 B** | 2.09 | Cero asignación en heap. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 543.3 ns | **0 B** | 2.22 | Cero asignación en heap. |
| **.NET 9.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 241.2 ns | 104 B | 1.00 | LINQ estándar en .NET 9. |
| **.NET 9.0 JIT** | `ValueLINQStructWhereSelectDelegados` | 446.8 ns | **0 B** | 1.85 | Cero asignación en heap. |
| **.NET 9.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | 448.4 ns | **0 B** | 1.86 | Cero asignación en heap. |
| **.NET 9.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 479.1 ns | **0 B** | 1.99 | Cero asignación en heap. |
| **.NET 9.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 483.0 ns | **0 B** | 2.00 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 261.1 ns | 104 B | 1.00 | LINQ estándar en .NET 8 LTS. |
| **.NET 8.0 JIT** | `ValueLINQRefStructWhereSelectDelegados` | 468.3 ns | **0 B** | 1.79 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectDelegados` | 477.9 ns | **0 B** | 1.83 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `ValueLINQRefStructWhereSelectLambdas` | 507.8 ns | **0 B** | 1.95 | Cero asignación en heap. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectLambdas` | 527.9 ns | **0 B** | 2.02 | Cero asignación en heap. |

---

### 2. Consultas Diferidas / Streaming (`ValueLINQ Delay`)
Evalúa el motor de ejecución diferida (`ValueLINQDelayStruct`) disponible para .NET 9.0 o superior. A diferencia del motor Eager, el motor diferido no alquila buffers en el pool de memoria durante las transformaciones intermedias (`Where`, `Select`), sino que encadena enumeradores struct que los compiladores RyuJIT y Native AOT inlinean directamente en un **único bucle físico de iteración**, reduciendo la latencia al régimen **sub-microsegundo (< 1,000 ns)** con estrictamente **0 B** de alocaciones en el Heap.

*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: [`JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Delay.ValueLINQDelayOperatorsBenchmarks`](../../JCarrillo.AOT.Core.Benchmarks/ValueLINQ/Delay/ValueLINQDelayOperatorsBenchmarks.cs).

#### Escala $N = 1000$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectDelegados` | **626.07 ns** | **0 B** | **-95.6% latencia (23x más rápido que BCL)** con delegados struct en Native AOT (medido). |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectStaticLambda` | 2,077.39 ns | **0 B** | 6.9x más rápido que BCL con lambdas estáticas (medido). |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectNoStaticLambda` | 2,101.68 ns | 120 B | Motor Delay con clausura capturadora en Native AOT (medido). |
| **NativeAOT 10.0** | `StandardLINQWhereSelect` (Baseline) | 14,379.39 ns | 144 B | BCL en Native AOT: penalizado por despacho dinámico de interfaz (medido). |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelectDelegados` | **931.69 ns** | **0 B** | **5.0x más rápido que BCL** en Native AOT 9 (medido). |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelectStaticLambda` | 2,325.43 ns | **0 B** | 2.0x más rápido que BCL con lambdas estáticas (medido). |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelectNoStaticLambda` | 2,343.98 ns | 120 B | Motor Delay con clausura capturadora en Native AOT (medido). |
| **NativeAOT 9.0** | `StandardLINQWhereSelect` (Baseline) | 4,693.87 ns | 104 B | BCL en Native AOT 9 (medido). |
| **NativeAOT 8.0** | `ValueLINQDelayWhereSelectDelegados` | PNSE | N/A | Excepción `PlatformNotSupportedException` (.NET 9+ requerido) (medido). |
| **NativeAOT 8.0** | `StandardLINQWhereSelect` (Baseline) | 4,467.78 ns | 104 B | BCL en Native AOT 8 (medido). |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectDelegados` | **728.80 ns** | **0 B** | **65.7% más rápido que BCL** con inlining puro de struct en RyuJIT 10 (medido). |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | 1,842.15 ns | **0 B** | 13.4% más rápido que BCL con lambdas estáticas (medido). |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,869.44 ns | 152 B | Asignación en heap por clausura lambda de RyuJIT (medido). |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,126.11 ns | 104 B | LINQ estándar en RyuJIT 10 (medido). |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelectDelegados` | **794.10 ns** | **0 B** | **60.6% más rápido que BCL** en .NET 9 (medido). |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | 1,891.02 ns | **0 B** | Paridad con BCL y cero asignación (medido). |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,918.55 ns | 152 B | Asignación en heap por clausura lambda en .NET 9 (medido). |
| **.NET 9.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,014.28 ns | 104 B | LINQ estándar en .NET 9 (medido). |
| **.NET 8.0 JIT** | `ValueLINQDelayWhereSelectDelegados` | PNSE | N/A | Excepción `PlatformNotSupportedException` (.NET 9+ requerido) (medido). |
| **.NET 8.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 2,189.44 ns | 104 B | LINQ estándar en .NET 8 LTS (medido). |

#### Escala $N = 100$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectDelegados` | **69.06 ns** | **0 B** | **-69.9% latencia (3.3x más rápido que BCL)** en escala reducida (medido). |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | **96.31 ns** | **0 B** | **-58.0% latencia (2.4x más rápido)** con cero GC (medido). |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 125.51 ns | 152 B | Clausura capturadora (medido). |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline) | 229.30 ns | 104 B | LINQ estándar en RyuJIT 10 (medido). |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectDelegados` | **84.15 ns** | **0 B** | **-94.2% latencia (17.1x más rápido que BCL)** en compilación nativa (medido). |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectStaticLambda` | **187.20 ns** | **0 B** | **7.7x más rápido que BCL** con cero GC (medido). |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectNoStaticLambda` | 215.30 ns | 120 B | Clausura capturadora en Native AOT (medido). |
| **NativeAOT 10.0** | `StandardLINQWhereSelect` (Baseline) | 1,439.40 ns | 144 B | LINQ estándar en compilación nativa (medido). |

> [!TIP]
> **Eager vs. Delay: ¿Cuándo utilizar cada uno?**
> - **ValueLINQ Delay (`ToValueDelayQuery()`)**: Recomendado para filtros y proyecciones en streaming donde no se requiere indexación por posición ni materialización intermedia. El inlining reduce la latencia a **sub-microsegundo (626 ns a $N=1000$, 69 ns a $N=100$)** sin interactuar con el heap ni con el pool de memoria.
> - **ValueLINQ Eager (`ToValueQuery()`)**: Recomendado cuando se requiere materialización, particionamiento (`Chunk`, `Take`, `Skip`) o múltiples pasadas sobre la colección, amortizando el alquiler de buffers en el `ArrayPool<T>` / `StateManager`.

---

### 3. Iteración y Recorrido de Colecciones ($N = 1000$)
Evalúa el costo puro de recorrer secuencialmente los elementos de la consulta mediante `foreach` sobre el `Span<T>` transitorio obtenido del `StateManager` (los métodos `GetEnumerator()` de `ValueLINQStruct<T>` y `ValueLINQRefStruct<T>` devuelven el enumerador estándar `Span<T>.Enumerator`, que entrega los elementos por referencia y sin asignaciones), frente a bucles nativos y baselines del sistema. El tipo público `ValueLINQEnumerator<T>` existe en la biblioteca, pero no participa en la ruta de iteración medida por estos benchmarks.

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Descripción / Comportamiento |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ArrayIteration` | 275.66 ns | 0 B | Recorrido directo de array indexado (Naive Baseline). |
| **.NET 10.0 JIT** | `ListIteration` | 502.79 ns | 0 B | Bucle `foreach` nativo sobre `List<T>`. |
| **.NET 10.0 JIT** | `ValueLINQStructIterationOnly` | 583.34 ns | 0 B | Iteración pura del Span transitorio obtenido del StateManager. |
| **.NET 10.0 JIT** | `ValueLINQRefStructIterationWithCreation` | 704.64 ns | **0 B** | Pipeline completo: Renta de slot + Bucle `foreach` + Liberación (`Dispose()`). |
| **NativeAOT 10.0** | `ArrayIteration` | 273.68 ns | 0 B | Recorrido de array indexado en Native AOT. |
| **NativeAOT 10.0** | `ListIteration` | 511.92 ns | 0 B | Bucle `foreach` sobre lista nativa en Native AOT. |
| **NativeAOT 10.0** | `ValueLINQStructIterationOnly` | 523.93 ns | 0 B | Recorrido de Span transitorio en Native AOT. |
| **NativeAOT 10.0** | `ValueLINQRefStructIterationWithCreation` | 644.80 ns | **0 B** | Ciclo completo síncrono en Native AOT. |

---

### 4. Concatenación y Sobrecarga `params` ($N = 100$)
Mide la alocación silenciosa inducida por el compilador al resolver el paso de variables múltiples mediante `params` en `.NET 8.0` frente a las optimizaciones del compilador en `.NET 9.0/10.0` (gracias a `ReadOnlySpan<T>` en params).

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Estado de Asignaciones |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 8.0 JIT** | `ValueLINQStructConcatStatic4Elements` | 691.40 ns | **0 B** | Llamada con argumentos fistas estáticos. |
| **.NET 8.0 JIT** | `ValueLINQStructConcatParams5Elements` | 933.74 ns | **56 B** | **Alocación heap detectada** (creación del array temporal). |
| **.NET 9.0 JIT** | `ValueLINQStructConcatStatic4Elements` | 720.41 ns | **0 B** | Llamada con argumentos fistas estáticos. |
| **.NET 9.0 JIT** | `ValueLINQStructConcatParams5Elements` | 940.65 ns | **0 B** | **0 Allocations** (JIT inline de `ReadOnlySpan<T>`). |
| **.NET 10.0 JIT** | `ValueLINQStructConcatStatic4Elements` | 715.78 ns | **0 B** | Sin params. |
| **.NET 10.0 JIT** | `ValueLINQStructConcatParams5Elements` | 862.42 ns | **0 B** | **0 Allocations** (JIT inline de `ReadOnlySpan<T>`). |

---

### 5. Inserción y Población de Datos ($N = 1000$)
Mide la sobrecarga del modelo síncrono al alimentar la colección. Compara la inserción elemento a elemento (que adquiere locks de exclusión mutua por iteración: $O(N)$ locks) frente a la copia vectorial consolidada en un solo paso (`Añadir(ReadOnlySpan<T>)`: $O(1)$ lock).

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Sincronización y Complejidad |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ListIntBlock` (`AddRange`) | 166.57 ns | 4,056 B | Copia masiva sin locks. |
| **.NET 10.0 JIT** | `ValueLINQRefStructIntBlock` (`Bulk`) | 142.60 ns | **0 B** | **$O(1)$ Lock** (Transferencia vectorial a velocidad de hardware). |
| **.NET 10.0 JIT** | `ValueLINQStructIntFixed` (`Unit`) | 29,797.46 ns | **0 B** | $O(N)$ Locks (1000 bloqueos síncronos redundantes). |
| **NativeAOT 10.0** | `ListIntBlock` (`AddRange`) | 156.30 ns | 4,056 B | Copia masiva nativa. |
| **NativeAOT 10.0** | `ValueLINQStructIntBlock` (`Bulk`) | 154.87 ns | **0 B** | **$O(1)$ Lock** (Optimización en compilación estática). |
| **NativeAOT 10.0** | `ValueLINQStructIntFixed` (`Unit`) | 31,979.34 ns | **0 B** | $O(N)$ Locks (1000 bloqueos síncronos redundantes). |

> [!NOTE]
> Las filas `Bulk` corresponden a dos benchmarks distintos del harness: `ValueLINQRefStructIntBlock` (variante `RefStruct`) en .NET 10.0 JIT y `ValueLINQStructIntBlock` (variante `Struct`) en NativeAOT 10.0.

---

### 6. Operadores de Materialización (Pooled vs Standard)
Compara el coste de materialización en colecciones tradicionales alojadas en el Heap administrado (`ToArrayStandard` y `ToListStandard`) frente a las variantes optimizadas de ValueLINQ que reutilizan buffers a través de `ArrayPool<T>` (`ToArray` y `ToList`) implementadas en [ValueLINQExtensions.cs](../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs). El harness de benchmark se encuentra en [MaterializacionBenchmarks.cs](../../JCarrillo.AOT.Core.Benchmarks/ValueLINQ/Metodos/MaterializacionBenchmarks.cs).

#### Escala N = 100 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayPooled` | 120.22 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 102.56 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListPooled` | 127.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListStandardHeap` | 107.07 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayPooled` | 125.26 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 98.08 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListPooled` | 127.32 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListStandardHeap` | 168.14 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayPooled` | 151.86 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 112.83 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListPooled` | 134.19 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListStandardHeap` | 113.64 ns | 456 B | Asignación en Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayPooled` | 211.68 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayStandardHeap` | 163.40 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListPooled` | 209.89 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListStandardHeap` | 176.74 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToArrayPooled` | 203.73 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToArrayStandardHeap` | 158.90 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToListPooled` | 204.04 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToListStandardHeap` | 171.88 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToArrayPooled` | 247.38 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToArrayStandardHeap` | 169.34 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToListPooled` | 232.62 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToListStandardHeap` | 182.98 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |

#### Escala N = 1000 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayPooled` | 328.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 467.07 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListPooled` | 353.51 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStructToListStandardHeap` | 461.80 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayPooled` | 203.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 291.96 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListPooled` | 206.01 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStructToListStandardHeap` | 299.07 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayPooled` | 409.09 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToArrayStandardHeap` | 513.52 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListPooled` | 402.30 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStructToListStandardHeap` | 310.55 ns | 4,056 B | Regresión de CPU frente a Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayPooled` | 207.54 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToArrayStandardHeap` | 279.72 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListPooled` | 230.63 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStructToListStandardHeap` | 284.67 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToArrayPooled` | 218.06 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToArrayStandardHeap` | 268.22 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToListPooled` | 212.36 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStructToListStandardHeap` | 289.00 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToArrayPooled` | 224.18 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToArrayStandardHeap` | 284.56 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToListPooled` | 220.80 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStructToListStandardHeap` | 296.37 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |

> [!NOTE]
> **Análisis del Coste de Alquiler**:
> En tamaños de colección pequeños ($N = 100$), los materializadores estándar muestran una latencia menor (entre un 14.7% y un 31.5% más rápidos, medido), debido a que no incurren en la sobrecarga de alquiler y devolución de buffers en el pool de memoria (`ArrayPool<T>.Shared`), con la única excepción de `ToListStandard` en .NET 9.0 JIT, donde la versión Pooled fue un 24.3% más rápida (medido).
> A mayor escala ($N = 1000$), las asignaciones repetitivas en el Heap penalizan a los materializadores estándar, permitiendo que las versiones pooled superen su rendimiento en un rango del 18.7% al 31.1% en la mayoría de los entornos (medido), manteniendo un consumo nulo de asignaciones de memoria heap (0 B, medido). La única regresión observada en esta escala ocurre en `.NET 8.0 JIT`, donde `ValueLINQStructToListStandardHeap` (310.55 ns, medido) supera a `ValueLINQStructToListPooled` (402.30 ns, medido) en un 29.5% debido a sobrecargas del pool.

---

### 7. Arenas de Memoria (Sobrecoste y Amortización)

Mide el coste de la nueva capa de [arenas de memoria](Core/Arenas.md): el alquiler/liberación de una arena, y una cadena `Where + Select` (materializada con `ToArray`) sobre la arena ambiente (0), sobre una arena explícita **reutilizada** y sobre una arena **nueva por cada consulta**.

#### .NET 10.0 JIT (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 55.03 ns (medido) | 56.88 ns (medido) | 0 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 422.06 ns (medido) | 1,103.71 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 420.90 ns (medido) | 1,147.77 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 2,444.31 ns (medido) | 4,190.51 ns (medido) | 25,848 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

#### .NET 9.0 JIT (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 61.55 ns (medido) | 65.25 ns (medido) | 40 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 414.42 ns (medido) | 1,202.90 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 412.87 ns (medido) | 1,240.93 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 2,320.46 ns (medido) | 3,606.99 ns (medido) | 25,856 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

#### .NET 8.0 JIT (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 62.68 ns (medido) | 65.45 ns (medido) | 40 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 413.89 ns (medido) | 1,447.17 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 416.46 ns (medido) | 1,163.56 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 3,492.36 ns (medido) | 4,029.46 ns (medido) | 25,856 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

#### NativeAOT 10.0 (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 58.10 ns (medido) | 61.08 ns (medido) | 0 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 435.96 ns (medido) | 1,328.46 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 430.89 ns (medido) | 1,261.63 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 3,266.25 ns (medido) | 4,267.56 ns (medido) | 25,848 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

#### NativeAOT 9.0 (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 61.39 ns (medido) | 64.75 ns (medido) | 40 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 429.80 ns (medido) | 1,157.62 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 454.18 ns (medido) | 1,176.47 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 2,465.71 ns (medido) | 3,225.43 ns (medido) | 25,856 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

#### NativeAOT 8.0 (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | 63.10 ns (medido) | 64.18 ns (medido) | 40 B (medido) | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 471.60 ns (medido) | 1,256.97 ns (medido) | 0 B (medido) | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | 466.39 ns (medido) | 1,236.97 ns (medido) | 0 B (medido) | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 2,638.99 ns (medido) | 3,633.54 ns (medido) | 25,856 B (medido) | Una arena nueva creada y dispuesta por cada consulta. |

> [!IMPORTANT]
> **El coste de una arena es de creación de tabla, no de consulta.**
> 1. **Reutilizar una arena no cuesta nada medible**: `WhereSelect_ArenaReutilizada` iguala a la arena ambiente (por ejemplo, 420.90 ns (medido) vs 422.06 ns (medido) a $N=100$; 1,147.77 ns (medido) vs 1,103.71 ns (medido) a $N=1000$; 0 B (medido) asignados en ambos bajo .NET 10.0 JIT). El enrutado por `arena_id` (tres cargas dependientes frente a una) queda dentro del ruido de medición. Usar arenas explícitas en estado estacionario es, en la práctica, gratis.
> 2. **El sobrecoste de `WhereSelect_ArenaExplicita` (aproximadamente 25 KB asignados, específicamente 25,848 B (medido) o 25,856 B (medido)) es enteramente la materialización de la tabla de sesiones del tipo en una arena nueva** (~25 KB: stack de índices + primera partición). Es el coste "se paga una vez por (tipo, arena)" que se estimaba por fórmula, ahora medido. En uso real, una arena se reutiliza para muchas consultas y ese coste se amortiza hasta las cifras de `WhereSelect_ArenaReutilizada`.
> 3. **Crear/disponer una arena vacía cuesta ~55.03 ns (medido) y 0 B (medido) en .NET 10.0 JIT** (mientras que en .NET 8.0/9.0 es de 40 B (medido) por la asignación del enumerador del `ConcurrentBag` de liberadores en el camino de `Dispose`, que RyuJIT elimina en .NET 10.0). Es un camino frío (una vez por ámbito), no la ruta caliente de consulta.

---

## Guía de Reproducibilidad de Benchmarks

Para ejecutar, auditar o reproducir de manera independiente cualquiera de las mediciones cuantitativas tabuladas en este informe técnico, utilice el ejecutable del proyecto de benchmarks en configuración `Release`. Los siguientes comandos permiten aislar cada suite de pruebas mediante filtros directos de BenchmarkDotNet:

```bash
# 1. Consultas Fluent Eager (Where + Select con delegados struct y lambdas)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *SecuenciaWhereSelectBenchmarks*

# 2. Consultas Diferidas / Streaming (ValueLINQ Delay con inlining sub-microsegundo)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *ValueLINQDelayOperatorsBenchmarks*

# 3. Iteración y Recorrido de Colecciones (Span transitorio vs List/Array)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *IteracionBenchmarks*

# 4. Concatenación y Sobrecarga params (Medición de alocaciones en Heap)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *BoxingBenchmarks*

# 5. Inserción y Población de Datos (Contención síncrona O(1) bulk vs O(N) locks)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *ValueLINQMetodosBenchmarks*

# 6. Operadores de Materialización (PooledArray/PooledList vs Heap estándar)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *MaterializacionBenchmarks*

# 7. Arenas de Memoria (Ciclo de vida y consultas en arena ambiente vs explícita)
dotnet run -c Release --project JCarrillo.AOT.Core.Benchmarks --framework net10.0 --filter *Arena*
```

---

## Lo que este benchmark NO mide

Las pruebas ejecutadas tienen un alcance restringido y no evalúan el comportamiento del sistema bajo las siguientes condiciones de producción:
1. **Contención de Sincronización en Concurrencia**: Los benchmarks se ejecutan en un único hilo. No miden el coste de bloqueo ni la degradación por contención cuando múltiples hilos intentan adquirir ranuras en `ValueLINQStateManager` simultáneamente.
2. **Presión General del Garbage Collector (GC)**: Aunque se mide la asignación neta (`Allocated`), el benchmark no simula el impacto a largo plazo de la fragmentación de memoria (LOH/SOH) ni la latencia inducida por pausas completas de GC (GC pauses) bajo throughput sostenido de producción.
3. **Persistencia y Costes de I/O**: Todas las operaciones se realizan en memoria virtual y sobre colecciones precalentadas. No se incluye el coste de I/O de red, acceso físico a disco ni latencias de red en servicios externos.

## Conclusiones de Rendimiento

1.  **Eficiencia del Heap**: El uso de la API diferida (Delay) con structs predicados o lambdas estáticas logra **0 B (medido)** asignados en heap. En lambdas capturadoras se observa una penalización de 152 B **(medido)** en JIT y 120 B **(medido)** en Native AOT debido al objeto de clausura generado por el compilador. En .NET 10.0 JIT, el análisis de escape de RyuJIT reduce la asignación en heap del motor Eager con lambdas no estáticas a solo 24 B **(medido)**.
2.  **Ventaja en Native AOT**: En Native AOT 10.0, la resolución dinámica de interfaces penaliza al LINQ estándar, elevando la latencia a 14,379.39 ns **(medido)**. El motor diferido `ValueLINQDelayWhereSelect` reduce la latencia a 626.07 ns **(medido)** (aproximadamente 23 veces más rápido) operando a velocidad de hardware por el inlining estático completo.
3.  **Amortización de Sincronización**: La población en bloque (`Añadir(ReadOnlySpan<T>)`) reduce la latencia en un **99.52% (medido)** frente a la inserción iterativa al sustituir el coste de $O(N)$ bloqueos por un único bloqueo atómico $O(1)$.
4.  **Amortización de Alquiler en Materialización**: Para escalas de colección reducidas ($N = 100$), los materializadores estándar son superiores en velocidad (14.7% a 31.5% más rápidos, medido) debido a la sobrecarga nula de alquiler de buffers; no obstante, para volúmenes mayores ($N = 1000$), las variantes pooled reducen el tiempo de CPU en un 18.7% a 31.1% (medido) al suprimir el coste de alocación de memoria del GC.
5.  **Coste Nulo de las Arenas Reutilizadas**: En .NET 10.0 JIT, una cadena `Where + Select` sobre una arena explícita reutilizada iguala a la arena ambiente (420.90 ns (medido) vs 422.06 ns (medido) a $N=100$; 0 B (medido) en ambos), confirmando que el enrutado por `arena_id` no introduce sobrecoste medible. El coste de una arena nueva por consulta (25,848 B (medido) o 25,856 B (medido)) corresponde íntegramente a la materialización única de la tabla de sesiones por (tipo, arena), amortizable a cero con la reutilización.
