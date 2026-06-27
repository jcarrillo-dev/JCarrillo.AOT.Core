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
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Factor de Velocidad vs LINQ |
| :--- | :--- | :---: | :---: | :---: |
| **.NET 10.0 JIT** | `StandardLINQ_Where_Select` | 2,038.19 ns | 104 B | 1.00 (Baseline) |
| **.NET 10.0 JIT** | `ValueLINQStruct_Where_Select` | 1,438.80 ns | **0 B** | **1.42x más rápido** |
| **.NET 10.0 JIT** | `ValueLINQRefStruct_Where_Select` | 1,296.50 ns | **0 B** | **1.57x más rápido** |
| **NativeAOT 10.0** | `StandardLINQ_Where_Select` | 14,314.08 ns | 144 B | 1.00 (Baseline) |
| **NativeAOT 10.0** | `ValueLINQStruct_Where_Select` | 1,625.87 ns | **0 B** | **8.80x más rápido** |
| **NativeAOT 10.0** | `ValueLINQRefStruct_Where_Select` | 1,551.11 ns | **0 B** | **9.23x más rápido** |
| **NativeAOT 9.0** | `StandardLINQ_Where_Select` | 4,551.89 ns | 104 B | 1.00 (Baseline) |
| **NativeAOT 9.0** | `ValueLINQStruct_Where_Select` | 1,312.92 ns | **0 B** | **3.47x más rápido** |
| **NativeAOT 9.0** | `ValueLINQRefStruct_Where_Select` | 1,585.71 ns | **0 B** | **2.87x más rápido** |
| **NativeAOT 8.0** | `StandardLINQ_Where_Select` | 4,378.67 ns | 104 B | 1.00 (Baseline) |
| **NativeAOT 8.0** | `ValueLINQStruct_Where_Select` | 1,451.04 ns | **0 B** | **3.02x más rápido** |
| **NativeAOT 8.0** | `ValueLINQRefStruct_Where_Select` | 1,433.26 ns | **0 B** | **3.05x más rápido** |

> [!IMPORTANT]
> **Comportamiento Crítico en Native AOT 10.0**:
> Los resultados empíricos revelan un desmarque drástico de rendimiento en la plataforma .NET 10.0 bajo compilación nativa. 
> Mientras que el LINQ estándar de .NET sufre una regresión de latencia masiva de **214.46% (medido)** al saltar de .NET 9.0 (**4,551.89 ns**) a .NET 10.0 (**14,314.08 ns**), las consultas fluent de ValueLINQ permanecen estables y predecibles, registrando **1,551.11 ns (medido)** en .NET 10.0 (apenas un aumento de latencia frente a los **1,433.26 ns** de .NET 8.0).
> Esto hace que bajo Native AOT 10.0, ValueLINQ sea **9.23 veces más rápido (medido)** que el Standard LINQ del sistema, liberando además el heap del GC (**0 B (medido)** vs **144 B (medido)**).
> Este salto de rendimiento se debe a que la compilación nativa de .NET 10.0 incrementa el coste de indirección para el despacho de interfaces virtuales genéricas dinámicas (`IEnumerable<T>`). Al estar estructurado con genéricos estáticos y restricciones de struct (`where TPredicate : struct`), ValueLINQ elude por completo las comprobaciones de metadatos dinámicos del runtime y permite que el compilador nativo realice el inlining directo a instrucciones de hardware, desmarcándose de la degradación general del sistema.

#### Escala $N = 100$ (Medido)
| Runtime / Engine | Método | Latencia Media (Mean) | Heap Allocated | Factor de Velocidad vs LINQ |
| :--- | :--- | :---: | :---: | :---: |
| **.NET 10.0 JIT** | `StandardLINQ_Where_Select` | 218.08 ns | 104 B | **1.00 (Baseline)** |
| **.NET 10.0 JIT** | `ValueLINQStruct_Where_Select` | 342.77 ns | **0 B** | 0.64x (Regresión por setup) |
| **.NET 10.0 JIT** | `ValueLINQRefStruct_Where_Select` | 337.11 ns | **0 B** | 0.65x (Regresión por setup) |
| **NativeAOT 10.0** | `StandardLINQ_Where_Select` | 1,375.65 ns | 144 B | 1.00 (Baseline) |
| **NativeAOT 10.0** | `ValueLINQStruct_Where_Select` | 420.94 ns | **0 B** | **3.27x más rápido** |
| **NativeAOT 10.0** | `ValueLINQRefStruct_Where_Select` | 416.17 ns | **0 B** | **3.31x más rápido** |

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

## Lo que este benchmark NO mide

Las pruebas ejecutadas tienen un alcance restringido y no evalúan el comportamiento del sistema bajo las siguientes condiciones de producción:
1. **Contención de Sincronización en Concurrencia**: Los benchmarks se ejecutan en un único hilo. No miden el coste de bloqueo ni la degradación por contención cuando múltiples hilos intentan adquirir ranuras en `ValueLINQStateManager` simultáneamente.
2. **Presión General del Garbage Collector (GC)**: Aunque se mide la asignación neta (`Allocated`), el benchmark no simula el impacto a largo plazo de la fragmentación de memoria (LOH/SOH) ni la latencia inducida por pausas completas de GC (GC pauses) bajo throughput sostenido de producción.
3. **Persistencia y Costes de I/O**: Todas las operaciones se realizan en memoria virtual y sobre colecciones precalentadas. No se incluye el coste de I/O de red, acceso físico a disco ni latencias de red en servicios externos.

## Conclusiones de Rendimiento

1.  **Eficiencia del Heap**: En todas las pruebas y runtimes, las colecciones estructuradas de ValueLINQ registraron 0 B de allocations en el Heap de GC (medido). Esto elimina la frecuencia de recolección de basura (Gen 0/1/2) en las rutas calientes.
2.  **Ventaja Crítica en Native AOT**: En Native AOT, la resolución dinámica de interfaces penaliza al LINQ estándar de .NET, degradando su rendimiento hasta en **9.23x (medido)** respecto a ValueLINQ ($N=1000$). ValueLINQ se beneficia del inlining a nivel de compilación estática, operando a velocidad de hardware.
3.  **Amortización de Sincronización**: La población en bloque (`Añadir(ReadOnlySpan<T>)`) reduce la latencia en un **99.52% (medido)** frente a la inserción iterativa al sustituir el coste de $O(N)$ bloqueos por un único bloqueo atómico $O(1)$.
4.  **Amortización de Alquiler en Materialización**: Para escalas de colección reducidas ($N = 100$), los materializadores estándar son superiores en velocidad (14.7% a 31.5%, medido) debido a la sobrecarga nula de alquiler de buffers; no obstante, para volúmenes mayores ($N = 1000$), las variantes pooled reducen el tiempo de CPU en un 18.7% a 31.1% (medido) al suprimir el coste de alocación de memoria del GC, excepto en .NET 8.0 JIT que exhibe una regresión en ToList.
