[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Select

El operador `Select` proyecta cada elemento de una colección en una nueva forma. Al igual que con `Where`, para evitar la asignación de delegados `Func<TSource, TResult>` en el Heap de GC y permitir el inlining total por parte del compilador, ValueLINQ utiliza un parámetro genérico struct que implementa la interfaz `ISelectDelegado<TOrigen, TResultado>`.

---

## 1. Firmas del Operador

El operador `Select` está disponible en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs) con las siguientes sobrecargas para los motores Eager y Delay:

### 1.1 Motor Eager (Alquiler de Búferes)

#### Con Struct Selector
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
    this ValueLINQStruct<TOrigen> origen, 
    TPredicate selector)
    where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
```
*(Disponible con firma homóloga para `ValueLINQRefStruct<T>`)*

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete("Usa structs delegados para evitar allocations. JCA0001")]
public static ValueLINQStruct<TResultado> Select<TOrigen, TResultado>(
    this ValueLINQStruct<TOrigen> origen, 
    Func<TOrigen, TResultado> selector)
```

### 1.2 Motor Lazy/Diferido (Delay)

#### Con Struct Selector (Paso por Referencia)
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQDelayStruct<TResultado, TEnumeratorResultado> Select<TOrigen, TEnumerator, TSelector, TResultado, TEnumeratorResultado>(
    this ValueLINQDelayStruct<TOrigen, TEnumerator> origen,
    ref TSelector selector)
    where TSelector : struct, ISelectDelegado<TOrigen, TResultado>
    where TEnumerator : struct, IEnumerator<TOrigen>
    where TEnumeratorResultado : struct, IEnumerator<TResultado>
```

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete("Usa structs delegados para evitar allocations. JCA0001")]
public static ValueLINQDelayStruct<TResultado, TEnumeratorResultado> Select<TOrigen, TEnumerator, TResultado, TEnumeratorResultado>(
    this ValueLINQDelayStruct<TOrigen, TEnumerator> origen,
    Func<TOrigen, TResultado> selector)
    where TEnumerator : struct, IEnumerator<TOrigen>
    where TEnumeratorResultado : struct, IEnumerator<TResultado>
```

### 1.3 Fallback de Plataforma (.NET 8.0)
El motor diferido (Delay) requiere soporte del runtime para la restricción genérica `allows ref struct` de C# 13. En ejecuciones bajo **.NET 8.0 / NativeAOT 8.0**, las llamadas a operadores Delay lanzarán una excepción `PlatformNotSupportedException` **(medido)** de forma limpia. Las variantes del motor Eager no tienen esta restricción y operan en todos los runtimes.

---

## 2. Abstracción del Selector

Para habilitar la compilación estática e inlining del código, los selectores de struct deben implementar la interfaz [ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface ISelectDelegado<TOrigen, TResultado>
    {
        TResultado Ejecutar(TOrigen objetoLista);
    }
}
```

---

## 3. Ejemplo de Uso Correcto

Implementación de un selector que multiplica cada número por dos:

```csharp
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.ValueLINQ;
using JCarrillo.AOT.Core.ValueLINQ.Interfaces;

public struct DuplicadorSelector : ISelectDelegado<int, int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Ejecutar(int numero)
    {
        return numero * 2;
    }
}

// Consumo en la aplicación
public static void EjecutarProyeccion(int[] datos)
{
    // Construir el pipeline de consulta, proyectar e iterar
    using var proyectados = datos
        .ToValueQuery()
        .Select<int, DuplicadorSelector, int>(new DuplicadorSelector());
    
    foreach (ref int numero in proyectados)
    {
        Console.WriteLine(numero);
    }
}
```

---

## 4. Análisis de Rendimiento (Mediciones)

Los datos de rendimiento corresponden a una prueba combinada de filtrado y proyección (`Where` + `Select`) con un tamaño de muestra $N = 1000$ (medido).

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

## 5. Justificación Técnica del Diseño

El operador `Select` de LINQ estándar (`IEnumerable<TResult>.Select`) introduce una indirección a través de un delegado dinámico y genera un objeto enumerador en el Heap de GC. En bucles críticos, esto impacta de dos formas:
1.  **Barrera de Inlining**: El compilador JIT/AOT no puede inlinear una llamada a través de un delegado `Func<TSource, TResult>` ya que el target del delegado solo se conoce en tiempo de ejecución. Esto añade la latencia de una llamada indirecta (`calli` a nivel de ensamblador) por cada elemento de la colección.
2.  **Presión en el GC**: La reserva continua de memoria del enumerador intermedio genera recolecciones frecuentes de Generación 0.

En cambio, `Select` en ValueLINQ utiliza restricciones genéricas sobre estructuras (`where TPredicate : struct`). Esto permite que el compilador Genérico genere una especialización física del método `Select` en tiempo de compilación. El método `Ejecutar` de la estructura se resuelve de forma estática, permitiendo que el compilador inserte el cuerpo del selector directamente dentro del bucle de procesamiento. Esto elimina la llamada indirecta por completo y resulta en una ejecución a velocidad de hardware nativo, lo que explica la diferencia en Native AOT donde ValueLINQ opera en 626.07 ns **(medido)** frente a los 14,379.39 ns **(medido)** del standard LINQ.

---
[Volver a Métodos y Extensiones](README.md)

