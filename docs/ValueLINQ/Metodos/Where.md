[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Where

El operador `Where` filtra los elementos de una colección basándose en un predicado estructurado de tipo `struct`. A diferencia del operador `Where` de LINQ estándar, que recibe un delegado `Func<T, bool>` (provocando asignaciones en el heap e impidiendo el inlining del JIT/AOT), ValueLINQ utiliza un parámetro genérico de tipo struct que implementa la interfaz `IWhereDelegado<TOrigen, TDato>`.

---

## 1. Firmas del Operador

El operador `Where` está disponible en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs) con las siguientes sobrecargas para los motores Eager y Delay:

### 1.1 Motor Eager (Alquiler de Búferes)

#### Con Struct Predicado
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
    this ValueLINQStruct<TOrigen> origen, 
    TDato dato, 
    TPredicate predicado)
    where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
```
*(Disponible con firma homóloga para `ValueLINQRefStruct<T>`)*

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete("Usa structs delegados para evitar allocations. JCA0001")]
public static ValueLINQStruct<TOrigen> Where<TOrigen>(
    this ValueLINQStruct<TOrigen> origen, 
    Func<TOrigen, bool> predicado)
```

### 1.2 Motor Lazy/Diferido (Delay)

#### Con Struct Predicado (Paso por Referencia)
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQDelayStruct<TOrigen, TEnumerator> Where<TOrigen, TEnumerator, TDato, TPredicate>(
    this ValueLINQDelayStruct<TOrigen, TEnumerator> origen,
    TDato dato,
    ref TPredicate predicado)
    where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
    where TEnumerator : struct, IEnumerator<TOrigen>
```

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete("Usa structs delegados para evitar allocations. JCA0001")]
public static ValueLINQDelayStruct<TOrigen, TEnumerator> Where<TOrigen, TEnumerator>(
    this ValueLINQDelayStruct<TOrigen, TEnumerator> origen,
    Func<TOrigen, bool> predicado)
    where TEnumerator : struct, IEnumerator<TOrigen>
```

### 1.3 Fallback de Plataforma (.NET 8.0)
El motor diferido (Delay) requiere soporte del runtime para la restricción genérica `allows ref struct` de C# 13. En ejecuciones bajo **.NET 8.0 / NativeAOT 8.0**, las llamadas a operadores Delay lanzarán una excepción `PlatformNotSupportedException` **(medido)** de forma limpia. Las variantes del motor Eager no tienen esta restricción y operan en todos los runtimes.

---

## 2. Abstracción del Predicado

Para habilitar la compilación estática e inlining del código, los predicados de struct deben implementar la interfaz [IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IWhereDelegado<TOrigen, TDato>
    {
        bool Ejecutar(TOrigen objetoLista, TDato otro);
    }
}
```

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
    public bool Ejecutar(int numero, int divisor)
    {
        return numero % divisor == 0;
    }
}

// Consumo en la aplicación
public static void EjecutarFiltro(int[] datos)
{
    // Construir el pipeline de consulta, filtrar e iterar
    using var filtrados = datos
        .ToValueQuery()
        .Where(2, new FiltroPares());
    
    foreach (ref int numero in filtrados)
    {
        Console.WriteLine(numero);
    }
}
```

---

## 4. Análisis de Rendimiento (Mediciones)

Los datos corresponden a una prueba combinada de filtrado y proyección (`Where` + `Select`) con un tamaño de muestra $N = 1000$ (medido).

*   **Entorno de Medición**: Windows 11, CPU AMD Ryzen 9 3950X (3.50GHz, 32 cores lógicos, 16 físicos) (medido).
*   **Harness**: BenchmarkDotNet v0.15.8, compilación en modo Release (medido).

### Tabla Comparativa de Rendimiento (Size = 1000)

| Runtime / Entorno | Método | Latencia Media (Mean) | Heap Allocated | Notas |
| :--- | :--- | :---: | :---: | :--- |
| **.NET 10.0 (JIT)** | `StandardLINQWhereSelect` (Baseline Realista) | 2,047.17 ns | 104 B | LINQ estándar del runtime. |
| **.NET 10.0 (JIT)** | `ValueLINQDelayWhereSelect` | 728.80 ns | 0 B | Motor Delay con structs puros. |
| **.NET 10.0 (JIT)** | `ValueLINQDelayWhereSelectStaticLambda` | 858.15 ns | 0 B | Motor Delay con lambdas estáticas. |
| **.NET 10.0 (JIT)** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,000.53 ns | 152 B | Motor Delay con clausura en lambda. |
| **.NET 10.0 (JIT)** | `ValueLINQStructWhereSelectStaticLambda` | 1,746.49 ns | 0 B | Motor Eager con lambda estática. |
| **.NET 10.0 (JIT)** | `ValueLINQStructWhereSelectNoStaticLambda` | 1,689.44 ns | 24 B | Motor Eager con optimización de escape RyuJIT. |
| **.NET 9.0 (JIT)** | `StandardLINQWhereSelect` (Baseline Realista) | 2,092.05 ns | 104 B | LINQ estándar del runtime. |
| **.NET 9.0 (JIT)** | `ValueLINQDelayWhereSelect` | 1,066.04 ns | 0 B | Motor Delay con structs puros. |
| **.NET 9.0 (JIT)** | `ValueLINQDelayWhereSelectStaticLambda` | 1,313.88 ns | 0 B | Motor Delay con lambdas estáticas. |
| **.NET 9.0 (JIT)** | `ValueLINQDelayWhereSelectNoStaticLambda` | 1,632.67 ns | 152 B | Motor Delay con clausura en lambda. |
| **.NET 8.0 (JIT)** | `StandardLINQWhereSelect` (Baseline Realista) | 2,144.24 ns | 104 B | LINQ estándar del runtime. |
| **.NET 8.0 (JIT)** | `ValueLINQDelayWhereSelect` | PNSE | N/A | Excepción PlatformNotSupportedException lanzada de forma limpia. |
| **.NET 8.0 (JIT)** | `ValueLINQStructWhereSelectStaticLambda` | 1,581.47 ns | 0 B | Motor Eager con lambda estática. |
| **.NET 8.0 (JIT)** | `ValueLINQStructWhereSelectNoStaticLambda` | 2,229.00 ns | 152 B | Motor Eager con clausura en lambda. |
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

---

## 5. Interpretación de los Datos

1. **Eficiencia en Pila (Delay pure structs)**: La combinación de `ToValueDelayQuery()` con predicados de struct puros (`ValueLINQDelayWhereSelect`) registra **0 B (medido)** asignados en heap. Su latencia en .NET 10.0 JIT es de 728.80 ns **(medido)**, frente a los 2,047.17 ns del LINQ estándar (un factor de velocidad de ~2.8x).
2. **Optimizaciones de Escape del Compilador**: Bajo .NET 10.0 JIT, `ValueLINQStructWhereSelectNoStaticLambda` aloca únicamente 24 B **(medido)** en heap frente a los 152 B del motor Delay equivalente. Esto es resultado de la optimización del análisis de escape en RyuJIT 10, que reduce la asignación del objeto closure a un espacio mínimo en heap para el motor Eager, mientras que en Native AOT se estabiliza en 120 B **(medido)**.
3. **Escenario Native AOT**: En NativeAOT 10.0, el coste de despacho dinámico en el baseline LINQ eleva su latencia a 14,379.39 ns **(medido)**. El motor diferido `ValueLINQDelayWhereSelect` opera en 626.07 ns **(medido)** con **0 B (medido)** asignados, resultando ~23 veces más rápido debido al inlining estático completo soportado por el compilador AOT.

---
[Volver a Métodos y Extensiones](README.md)

