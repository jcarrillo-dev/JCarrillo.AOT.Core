# Operador Where

El operador `Where` filtra los elementos de una colección basándose en un predicado estructurado de tipo `struct`. A diferencia del operador `Where` de LINQ estándar, que recibe un delegado `Func<T, bool>` (provocando asignaciones en el heap e impidiendo el inlining del JIT/AOT), ValueLINQ utiliza un parámetro genérico de tipo struct que implementa la interfaz `IWhereDelegado<TOrigen, TDato>`.

---

## 1. Firmas del Operador

El operador está disponible en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs) con las siguientes sobrecargas:

### Para `ValueLINQRefStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQRefStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
    this ValueLINQRefStruct<TOrigen> origen, 
    TDato dato, 
    TPredicate predicado)
    where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
```

### Para `ValueLINQStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
    this ValueLINQStruct<TOrigen> origen, 
    TDato dato, 
    TPredicate predicado)
    where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
```

---

## 2. Abstracción del Predicado

Para habilitar la compilación estática e inlining del código, el predicado debe ser una estructura que implemente la interfaz [IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IWhereDelegado<TOrigen, TDato>
    {
        bool Ejectuar(TOrigen objetoLista, TDato otro);
    }
}
```

*Nota: La interfaz define el método con el nombre exacto `Ejectuar`.*

---

## 3. Ejemplo de Uso Correcto

Implementación del filtro de números pares:

```csharp
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

public struct FiltroPares : IWhereDelegado<int, int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Ejectuar(int numero, int divisor)
    {
        return numero % divisor == 0;
    }
}

// Consumo en la aplicación
public static void EjecutarFiltro(int[] datos)
{
    // Construir el pipeline de consulta fluent, filtrar e iterar sobre los resultados de forma eficiente
    using var filtrados = datos
        .ToValueQuery()               // 1. Renta un búfer del ArrayPool y crea la sesión activa en el StateManager
        .Where(2, new FiltroPares()); // 2. Filtra números divisibles por 2 en pila con inlining estático del predicado
    
    foreach (ref int numero in filtrados)
    {
        Console.WriteLine(numero);
    }
    // Al finalizar el bloque, el using de 'filtrados' ejecuta automáticamente el Dispose() en cascada
}
```

---

## 4. Análisis de Rendimiento (Mediciones)

Los siguientes datos de rendimiento corresponden a la ejecución combinada de un filtro `Where` seguido de una proyección `Select` (`Where_Select`) con un tamaño de muestra $N = 100$ y $N = 1000$.

*   **Entorno de Medición**: Windows 11, CPU AMD Ryzen 9 3950X (3.50GHz, 1 CPU, 32 cores lógicos, 16 físicos), .NET SDK 10.0.301.
*   **Harness**: BenchmarkDotNet v0.15.8, compilación en modo Release.

### Tabla Comparativa de Rendimiento (p50/Mean)

| Runtime / Entorno | Escala (N) | Método | Latencia Media (medido) | Memoria Asignada (medido) | Relación de Latencia vs Standard (medido) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **.NET 10.0 (RyuJIT X64)** | 100 | `StandardLINQ_Where_Select` | 219.79 ns | 104 B | 1.00 (Baseline) |
| **.NET 10.0 (RyuJIT X64)** | 100 | `ValueLINQStruct_Where_Select` | 366.92 ns | 0 B | 1.67 |
| **.NET 10.0 (RyuJIT X64)** | 100 | `ValueLINQRefStruct_Where_Select` | 346.39 ns | 0 B | 1.58 |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `StandardLINQ_Where_Select` | 2,029.38 ns | 104 B | 1.00 (Baseline) |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `ValueLINQStruct_Where_Select` | 1,271.76 ns | 0 B | 0.63 |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `ValueLINQRefStruct_Where_Select` | 1,276.47 ns | 0 B | 0.63 |
| **NativeAOT 10.0 (X64)** | 100 | `StandardLINQ_Where_Select` | 1,705.71 ns | 144 B | 1.00 (Baseline) |
| **NativeAOT 10.0 (X64)** | 100 | `ValueLINQStruct_Where_Select` | 519.72 ns | 0 B | 0.30 |
| **NativeAOT 10.0 (X64)** | 100 | `ValueLINQRefStruct_Where_Select` | 526.09 ns | 0 B | 0.31 |
| **NativeAOT 10.0 (X64)** | 1000 | `StandardLINQ_Where_Select` | 15,826.87 ns | 144 B | 1.00 (Baseline) |
| **NativeAOT 10.0 (X64)** | 1000 | `ValueLINQStruct_Where_Select` | 2,022.59 ns | 0 B | 0.13 |
| **NativeAOT 10.0 (X64)** | 1000 | `ValueLINQRefStruct_Where_Select` | 2,040.27 ns | 0 B | 0.13 |

---

## 5. Interpretación de los Datos

1.  **Reducción Absoluta de Allocations**: Tanto `ValueLINQStruct` como `ValueLINQRefStruct` registran **0 bytes (medido)** de allocations en el Heap del GC en todas las ejecuciones, frente a los 104-144 bytes de LINQ estándar generados por la instanciación de clases enumeradoras y la captura de variables en clausuras.
2.  **Comportamiento en Escala ($N=1000$)**: Para colecciones pequeñas ($N=100$) en JIT, el LINQ estándar aprovecha optimizaciones de RyuJIT logrando menor latencia inicial. Sin embargo, al escalar a $N=1000$, ValueLINQ resulta un **37% más rápido (medido)** (reduciendo la latencia de 2,029.38 ns a 1,271.76 ns) gracias al inlining completo del predicado estructurado y la ausencia de indirecciones.
3.  **Ventaja Crítica bajo Native AOT**: En escenarios Native AOT, la diferencia es sustancial. `StandardLINQ` sufre una severa penalización de latencia (escalando hasta 15,826.87 ns para $N=1000$) debido al coste del despacho de interfaces dinámicas y metadatos no compilados estáticamente. ValueLINQ mantiene una latencia altamente predecible y optimizada de **2,022.59 ns (medido)**, lo cual representa una reducción de latencia del **87.2% (medido)** (casi 8 veces más rápido).
