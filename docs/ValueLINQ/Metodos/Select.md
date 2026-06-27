[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Select

El operador `Select` proyecta cada elemento de una colección en una nueva forma. Al igual que con `Where`, para evitar la asignación de delegados `Func<TSource, TResult>` en el Heap de GC y permitir el inlining total por parte del compilador, ValueLINQ utiliza un parámetro genérico struct que implementa la interfaz `ISelectDelegado<TOrigen, TResultado>`.

---

## 1. Firmas del Operador

El operador está disponible en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs) con las siguientes sobrecargas:

### Para `ValueLINQRefStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQRefStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
    this ValueLINQRefStruct<TOrigen> origen, 
    TPredicate selector)
    where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
```

### Para `ValueLINQStruct<T>`
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
    this ValueLINQStruct<TOrigen> origen, 
    TPredicate selector)
    where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
```

---

## 2. Abstracción del Selector

Para habilitar la compilación estática e inlining del código, el selector de proyección debe ser una estructura que implemente la interfaz [ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface ISelectDelegado<TOrigen, TResultado>
    {
        TResultado Ejecutar(TOrigen objetoLista);
    }
}
```

*Nota: La interfaz define el método con el nombre exacto `Ejecutar`.*

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
    // Construir el pipeline de consulta fluent, proyectar e iterar sobre los resultados
    using var proyectados = datos
        .ToValueQuery()                                                 // 1. Renta un búfer del ArrayPool y crea la sesión activa en el StateManager
        .Select<int, DuplicadorSelector, int>(new DuplicadorSelector()); // 2. Multiplica cada elemento por 2 con inlining estático de la proyección
    
    foreach (ref int numero in proyectados)
    {
        Console.WriteLine(numero);
    }
    // Al finalizar el bloque, el using de 'proyectados' ejecuta automáticamente el Dispose() en cascada
}
```

---

## 4. Análisis de Rendimiento (Mediciones)

Los datos de rendimiento se midieron en una consulta combinada de filtrado y proyección (`Where_Select`) para evaluar el impacto real en el pipeline.

*   **Entorno de Medición**: Windows 11, CPU AMD Ryzen 9 3950X (3.50GHz, 1 CPU, 32 cores lógicos, 16 físicos), .NET SDK 10.0.301.
*   **Harness**: BenchmarkDotNet v0.15.8, compilación en modo Release.

### Tabla Comparativa de Rendimiento (p50/Mean)

| Runtime / Entorno | Muestra (N) | Método | Latencia Media (medido) | Memoria Asignada (medido) | Relación de Latencia vs Standard (medido) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **.NET 10.0 (RyuJIT X64)** | 100 | `StandardLINQ_Where_Select` | 219.79 ns | 104 B | 1.00 (Baseline) |
| **.NET 10.0 (RyuJIT X64)** | 100 | `ValueLINQStruct_Where_Select` | 366.92 ns | 0 B | 1.67 |
| **.NET 10.0 (RyuJIT X64)** | 100 | `ValueLINQRefStruct_Where_Select` | 346.39 ns | 0 B | 1.58 |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `StandardLINQ_Where_Select` | 2,029.38 ns | 104 B | 1.00 (Baseline) |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `ValueLINQStruct_Where_Select` | 1,271.76 ns | 0 B | 0.63 |
| **.NET 10.0 (RyuJIT X64)** | 1000 | `ValueLINQRefStruct_Where_Select` | 1,276.47 ns | 0 B | 0.63 |
| **NativeAOT 10.0 (X64)** | 1000 | `StandardLINQ_Where_Select` | 15,826.87 ns | 144 B | 1.00 (Baseline) |
| **NativeAOT 10.0 (X64)** | 1000 | `ValueLINQStruct_Where_Select` | 2,022.59 ns | 0 B | 0.13 |
| **NativeAOT 10.0 (X64)** | 1000 | `ValueLINQRefStruct_Where_Select` | 2,040.27 ns | 0 B | 0.13 |

---

## 5. Justificación Técnica del Diseño

El operador `Select` de LINQ estándar (`IEnumerable<TResult>.Select`) introduce una indirección a través de un delegado dinámico y genera un objeto enumerador en el Heap de GC. En bucles críticos, esto impacta de dos formas:
1.  **Barrera de Inlining**: El compilador JIT/AOT no puede inlinear una llamada a través de un delegado `Func<TSource, TResult>` ya que el target del delegado solo se conoce en tiempo de ejecución. Esto añade la latencia de una llamada indirecta (`calli` a nivel de ensamblador) por cada elemento de la colección.
2.  **Presión en el GC**: La reserva continua de memoria del enumerador intermedio genera recolecciones frecuentes de Generación 0.

En cambio, `Select` en ValueLINQ utiliza restricciones genéricas sobre estructuras (`where TPredicate : struct`). Esto permite que el compilador Genérico genere una especialización física del método `Select` en tiempo de compilación. El método `Ejecutar` de la estructura se resuelve de forma estática, permitiendo que el compilador inserte el cuerpo del selector directamente dentro del bucle de procesamiento. Esto elimina la llamada indirecta por completo y resulta en una ejecución a velocidad de hardware nativo.

---
[Volver a Métodos y Extensiones](README.md)

