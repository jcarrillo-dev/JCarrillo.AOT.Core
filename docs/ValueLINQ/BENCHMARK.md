[Volver al Sitemap de Documentación](../README.md) | [Volver a ValueLINQ](README.md)

# Reporte de Benchmarks Consolidado de ValueLINQ


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

## Tablas de Resultados Comparativos (Medidos)

### 1. Consultas Fluent (`Where` + `Select`)
Prueba que simula un pipeline común de procesamiento de datos compuesto por un filtrado y una proyección en cadena.

#### Escala $N = 1000$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline Realista) | 2,047.17 ns | 104 B | LINQ estándar del runtime. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelect` | 728.80 ns | 0 B | Motor Delay con structs puros. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | 858.15 ns | 0 B | Motor Delay con lambdas estáticas. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,000.53 ns | 152 B | Motor Delay con clausura en lambda. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectStaticLambda` | 1,746.49 ns | 0 B | Motor Eager con lambda estática. |
| **.NET 10.0 JIT** | `ValueLINQStructWhereSelectNoStaticLambda` | 1,689.44 ns | 24 B | Motor Eager con optimización de escape RyuJIT 10. |
| **.NET 9.0 JIT** | `StandardLINQWhereSelect` (Baseline Realista) | 2,092.05 ns | 104 B | LINQ estándar del runtime. |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelect` | 1,066.04 ns | 0 B | Motor Delay con structs puros. |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | 1,313.88 ns | 0 B | Motor Delay con lambdas estáticas. |
| **.NET 9.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,632.67 ns | 152 B | Motor Delay con clausura en lambda. |
| **.NET 8.0 JIT** | `StandardLINQWhereSelect` (Baseline Realista) | 2,144.24 ns | 104 B | LINQ estándar del runtime. |
| **.NET 8.0 JIT** | `ValueLINQDelayWhereSelect` | PNSE | N/A | Excepción PlatformNotSupportedException lanzada de forma limpia. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectStaticLambda` | 1,581.47 ns | 0 B | Motor Eager con lambda estática. |
| **.NET 8.0 JIT** | `ValueLINQStructWhereSelectNoStaticLambda` | 2,229.00 ns | 152 B | Motor Eager con clausura en lambda. |
| **NativeAOT 10.0** | `StandardLINQWhereSelect` (Baseline Realista) | 14,379.39 ns | 144 B | LINQ estándar en compilación nativa. |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelect` | 626.07 ns | 0 B | Motor Delay con structs puros. |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectStaticLambda` | 2,077.39 ns | 0 B | Motor Delay con lambdas estáticas. |
| **NativeAOT 10.0** | `ValueLINQDelayWhereSelectNoStaticLambda` | 2,101.68 ns | 120 B | Motor Delay con clausura (Native AOT runtime). |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectStaticLambda` | 2,871.53 ns | 0 B | Motor Eager con lambda estática. |
| **NativeAOT 10.0** | `ValueLINQStructWhereSelectNoStaticLambda` | 3,145.85 ns | 120 B | Motor Eager con clausura (Native AOT runtime). |
| **NativeAOT 9.0** | `StandardLINQWhereSelect` (Baseline Realista) | 4,693.87 ns | 104 B | LINQ estándar en compilación nativa. |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelect` | 931.69 ns | 0 B | Motor Delay con structs puros. |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelectStaticLambda` | 2,325.43 ns | 0 B | Motor Delay con lambdas estáticas. |
| **NativeAOT 9.0** | `ValueLINQDelayWhereSelectNoStaticLambda` | 2,343.98 ns | 120 B | Motor Delay con clausura (Native AOT runtime). |
| **NativeAOT 8.0** | `StandardLINQWhereSelect` (Baseline Realista) | 4,467.78 ns | 104 B | LINQ estándar en compilación nativa. |
| **NativeAOT 8.0** | `ValueLINQDelayWhereSelect` | PNSE | N/A | Excepción PlatformNotSupportedException lanzada de forma limpia. |
| **NativeAOT 8.0** | `ValueLINQStructWhereSelectStaticLambda` | 2,996.83 ns | 0 B | Motor Eager con lambda estática. |
| **NativeAOT 8.0** | `ValueLINQStructWhereSelectNoStaticLambda` | 3,069.94 ns | 120 B | Motor Eager con clausura (Native AOT runtime). |

> [!IMPORTANT]
> **Comportamiento en Native AOT 10.0**:
> Los resultados empíricos revelan una diferencia sustancial en la plataforma .NET 10.0 bajo compilación nativa.
> Mientras que el LINQ estándar de .NET sufre una regresión de latencia en Native AOT 10.0 en comparación con Native AOT 9.0 (alcanzando 14,379.39 ns **(medido)**), la variante `ValueLINQDelayWhereSelect` registra una latencia de 626.07 ns **(medido)** con **0 B (medido)** asignados en heap. Esto se debe a que ValueLINQ elude el despacho de interfaces virtuales genéricas dinámicas (`IEnumerable<T>`) mediante el uso de especialización estática de tipos struct genéricos, lo cual permite al compilador nativo inlinear la lógica del usuario directamente en el bucle físico de ejecución.

#### Escala $N = 100$ (Medido)

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `StandardLINQWhereSelect` (Baseline Realista) | 229.30 ns | 104 B | LINQ estándar del runtime. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelect` | 69.06 ns | 0 B | Motor Delay con structs puros. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectStaticLambda` | 96.31 ns | 0 B | Motor Delay con lambdas estáticas. |
| **.NET 10.0 JIT** | `ValueLINQDelayWhereSelectNoStaticLambda` | 125.51 ns | 152 B | Motor Delay con clausura en lambda. |

---

### 2. Iteración y Recorrido de Colecciones ($N = 1000$)
Evalúa el costo puro de recorrer secuencialmente los elementos de la consulta mediante el enumerador estructurado `ValueLINQEnumerator<T>` frente a bucles nativos y baselines del sistema.

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Descripción / Comportamiento |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `Array_Iteration` | 275.66 ns | 0 B | Recorrido directo de array indexado (Naive Baseline). |
| **.NET 10.0 JIT** | `List_Iteration` | 502.79 ns | 0 B | Bucle `foreach` nativo sobre `List<T>`. |
| **.NET 10.0 JIT** | `ValueLINQStruct_Iteration_Only` | 583.34 ns | 0 B | Iteración pura del Span transitorio obtenido del StateManager. |
| **.NET 10.0 JIT** | `ValueLINQRefStruct_Iteration_WithCreation` | 704.64 ns | **0 B** | Pipeline completo: Renta de slot + Bucle `foreach` + Liberación (`Dispose()`). |
| **NativeAOT 10.0** | `Array_Iteration` | 273.68 ns | 0 B | Recorrido de array indexado en Native AOT. |
| **NativeAOT 10.0** | `List_Iteration` | 511.92 ns | 0 B | Bucle `foreach` sobre lista nativa en Native AOT. |
| **NativeAOT 10.0** | `ValueLINQStruct_Iteration_Only` | 523.93 ns | 0 B | Recorrido de Span transitorio en Native AOT. |
| **NativeAOT 10.0** | `ValueLINQRefStruct_Iteration_WithCreation` | 644.80 ns | **0 B** | Ciclo completo síncrono en Native AOT. |

---

### 3. Concatenación y Sobrecarga `params` ($N = 100$)
Mide la alocación silenciosa inducida por el compilador al resolver el paso de variables múltiples mediante `params` en `.NET 8.0` frente a las optimizaciones del compilador en `.NET 9.0/10.0` (gracias a `ReadOnlySpan<T>` en params).

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Estado de Asignaciones |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 8.0 JIT** | `ValueLINQStruct_Concat_Static_4Elements` | 691.40 ns | **0 B** | Llamada con argumentos fistas estáticos. |
| **.NET 8.0 JIT** | `ValueLINQStruct_Concat_Params_5Elements` | 933.74 ns | **56 B** | **Alocación heap detectada** (creación del array temporal). |
| **.NET 9.0 JIT** | `ValueLINQStruct_Concat_Static_4Elements` | 720.41 ns | **0 B** | Llamada con argumentos fistas estáticos. |
| **.NET 9.0 JIT** | `ValueLINQStruct_Concat_Params_5Elements` | 940.65 ns | **0 B** | **0 Allocations** (JIT inline de `ReadOnlySpan<T>`). |
| **.NET 10.0 JIT** | `ValueLINQStruct_Concat_Static_4Elements` | 715.78 ns | **0 B** | Sin params. |
| **.NET 10.0 JIT** | `ValueLINQStruct_Concat_Params_5Elements` | 862.42 ns | **0 B** | **0 Allocations** (JIT inline de `ReadOnlySpan<T>`). |

---

### 4. Inserción y Población de Datos ($N = 1000$)
Mide la sobrecarga del modelo síncrono al alimentar la colección. Compara la inserción elemento a elemento (que adquiere locks de exclusión mutua por iteración: $O(N)$ locks) frente a la copia vectorial consolidada en un solo paso (`Añadir(ReadOnlySpan<T>)`: $O(1)$ lock).

| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Sincronización y Complejidad |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `List_Int_Block` (`AddRange`) | 166.57 ns | 4,056 B | Copia masiva sin locks. |
| **.NET 10.0 JIT** | `ValueLINQRefStruct_Int_Block` (`Bulk`) | 142.60 ns | **0 B** | **$O(1)$ Lock** (Transferencia vectorial a velocidad de hardware). |
| **.NET 10.0 JIT** | `ValueLINQStruct_Int_Fixed` (`Unit`) | 29,797.46 ns | **0 B** | $O(N)$ Locks (1000 bloqueos síncronos redundantes). |
| **NativeAOT 10.0** | `List_Int_Block` (`AddRange`) | 156.30 ns | 4,056 B | Copia masiva nativa. |
| **NativeAOT 10.0** | `ValueLINQStruct_Int_Block` (`Bulk`) | 154.87 ns | **0 B** | **$O(1)$ Lock** (Optimización en compilación estática). |
| **NativeAOT 10.0** | `ValueLINQStruct_Int_Fixed` (`Unit`) | 31,979.34 ns | **0 B** | $O(N)$ Locks (1000 bloqueos síncronos redundantes). |

---

### 5. Operadores de Materialización (Pooled vs Standard)
Compara el coste de materialización en colecciones tradicionales alojadas en el Heap administrado (`ToArrayStandard` y `ToListStandard`) frente a las variantes optimizadas de ValueLINQ que reutilizan buffers a través de `ArrayPool<T>` (`ToArray` y `ToList`) implementadas en [ValueLINQExtensions.cs](../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs). El harness de benchmark se encuentra en [ValueLINQBenchmarks.cs](../../JCarrillo.AOT.Core.Benchmarks/Extensiones/ValueLINQBenchmarks.cs).

#### Escala N = 100 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 120.22 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 102.56 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 127.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 107.07 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 125.26 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 98.08 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 127.32 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 168.14 ns | 456 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 151.86 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 112.83 ns | 424 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 134.19 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 113.64 ns | 456 B | Asignación en Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArray_Pooled` | 211.68 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 163.40 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToList_Pooled` | 209.89 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToListStandard_Heap` | 176.74 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToArray_Pooled` | 203.73 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 158.90 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToList_Pooled` | 204.04 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToListStandard_Heap` | 171.88 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToArray_Pooled` | 247.38 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 169.34 ns | 424 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToList_Pooled` | 232.62 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToListStandard_Heap` | 182.98 ns | 456 B | Asignación en Heap (compilación nativa) (medido) |

#### Escala N = 1000 (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 328.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 467.07 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 353.51 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 10.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 461.80 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 203.42 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 291.96 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 206.01 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 9.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 299.07 ns | 4,056 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArray_Pooled` | 409.09 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToArrayStandard_Heap` | 513.52 ns | 4,024 B | Asignación en Heap administrado (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToList_Pooled` | 402.30 ns | 0 B | Buffer reciclado desde el pool (medido) |
| **.NET 8.0 JIT** | `ValueLINQStruct_ToListStandard_Heap` | 310.55 ns | 4,056 B | Regresión de CPU frente a Heap administrado (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArray_Pooled` | 207.54 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 279.72 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToList_Pooled` | 230.63 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 10.0** | `ValueLINQStruct_ToListStandard_Heap` | 284.67 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToArray_Pooled` | 218.06 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 268.22 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToList_Pooled` | 212.36 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 9.0** | `ValueLINQStruct_ToListStandard_Heap` | 289.00 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToArray_Pooled` | 224.18 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToArrayStandard_Heap` | 284.56 ns | 4,024 B | Asignación en Heap (compilación nativa) (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToList_Pooled` | 220.80 ns | 0 B | Buffer reciclado en compilación nativa (medido) |
| **NativeAOT 8.0** | `ValueLINQStruct_ToListStandard_Heap` | 296.37 ns | 4,056 B | Asignación en Heap (compilación nativa) (medido) |

> [!NOTE]
> **Análisis del Coste de Alquiler**:
> En tamaños de colección pequeños ($N = 100$), los materializadores estándar muestran una latencia menor (entre un 14.7% y un 31.5% más rápidos, medido), debido a que no incurren en la sobrecarga de alquiler y devolución de buffers en el pool de memoria (`ArrayPool<T>.Shared`), con la única excepción de `ToListStandard` en .NET 9.0 JIT, donde la versión Pooled fue un 24.3% más rápida (medido).
> A mayor escala ($N = 1000$), las asignaciones repetitivas en el Heap penalizan a los materializadores estándar, permitiendo que las versiones pooled superen su rendimiento en un rango del 18.7% al 31.1% en la mayoría de los entornos (medido), manteniendo un consumo nulo de asignaciones de memoria heap (0 B, medido). La única regresión observada en esta escala ocurre en `.NET 8.0 JIT`, donde `ToListStandard_Heap` (310.55 ns, medido) supera a `ToList_Pooled` (402.30 ns, medido) en un 29.5% debido a sobrecargas del pool.

---

## 4. Arenas de Memoria (Sobrecoste y Amortización)

Mide el coste de la nueva capa de [arenas de memoria](Core/Arenas.md): el alquiler/liberación de una arena, y una cadena `Where + Select` (materializada con `ToArray`) sobre la arena ambiente (0), sobre una arena explícita **reutilizada** y sobre una arena **nueva por cada consulta**.

> [!NOTE]
> **Alcance de esta pasada (medido)**: BenchmarkDotNet v0.15.8, `MemoryDiagnoser`, mismo hardware que el resto del reporte (AMD Ryzen 9 3950X). Ejecutado en **JIT para .NET 8.0 / 9.0 / 10.0**; las variantes **NativeAOT** de esta tabla quedan pendientes de la sesión de benchmarks completa. El resto de tablas de este documento sí incluyen NativeAOT.

### .NET 10.0 JIT (Medido)

| Método | $N=100$ | $N=1000$ | Heap Allocated | Notas |
| :--- | :---: | :---: | :---: | :--- |
| `CrearYDisponerArena` | **56.3 ns (medido)** | **56.4 ns (medido)** | **0 B (medido)** | Alquilar + liberar una arena vacía (sin sesiones). |
| `WhereSelect_Ambiente` (baseline) | 421 ns (medido) | 1,144 ns (medido) | **0 B (medido)** | Cadena en la arena ambiente (0), cuya tabla se crea al arranque. |
| `WhereSelect_ArenaReutilizada` | **430 ns (medido)** | **1,130 ns (medido)** | **0 B (medido)** | Misma cadena en una arena explícita ya materializada y reutilizada. |
| `WhereSelect_ArenaExplicita` | 3,640 ns (medido) | 3,858 ns (medido) | **25,848 B (medido)** | Una arena **nueva** creada y dispuesta por cada consulta. |

> [!IMPORTANT]
> **El coste de una arena es de creación de tabla, no de consulta.**
> 1. **Reutilizar una arena no cuesta nada medible**: `WhereSelect_ArenaReutilizada` iguala a la arena ambiente (430 vs 421 ns a $N=100$; 1,130 vs 1,144 ns a $N=1000$; **0 B** en ambos, medido). El enrutado por `arena_id` (tres cargas dependientes frente a una) queda dentro del ruido de medición. Usar arenas explícitas en estado estacionario es, en la práctica, **gratis**.
> 2. **El sobrecoste de `WhereSelect_ArenaExplicita` (los 25,848 B y el ×3–9 de latencia, medido) es enteramente la materialización de la tabla de sesiones del tipo en una arena nueva** (~25 KB: stack de índices + primera partición). Es el coste "se paga una vez por (tipo, arena)" que se estimaba por fórmula, ahora **medido**. En uso real, una arena se reutiliza para muchas consultas y ese coste se amortiza hasta las cifras de `ArenaReutilizada`.
> 3. **Crear/disponer una arena vacía cuesta ~56 ns y 0 B (medido) en .NET 10.0** (en .NET 8.0/9.0 JIT se observan **40 B (medido)** por la asignación del enumerador del `ConcurrentBag` de liberadores en el camino de `Dispose`, que RyuJIT elimina en .NET 10.0). Es un camino frío (una vez por ámbito), no la ruta caliente de consulta.

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
5.  **Coste Nulo de las Arenas Reutilizadas**: En .NET 10.0 JIT, una cadena `Where + Select` sobre una arena explícita reutilizada iguala a la arena ambiente (**430 ns vs 421 ns a $N=100$; 0 B en ambos, medido**), confirmando que el enrutado por `arena_id` no introduce sobrecoste medible. El coste de una arena nueva por consulta (**25,848 B, medido**) corresponde íntegramente a la materialización única de la tabla de sesiones por (tipo, arena), amortizable a cero con la reutilización.
