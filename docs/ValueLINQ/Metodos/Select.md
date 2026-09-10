[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Select

El operador `Select` proyecta cada elemento de una colección en una nueva forma. Al igual que con `Where`, para evitar la asignación de delegados `Func<TSource, TResult>` en el Heap de GC y permitir el inlining total por parte del compilador, ValueLINQ utiliza un parámetro genérico struct que implementa la interfaz `ISelectDelegado<TOrigen, TResultado>`.

---

## 1. Firmas del Operador

Las sobrecargas del operador `Select` están repartidas en varios archivos según el motor y el estilo de invocación:

- **Eager con struct selector**: [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs), con sobrecargas homólogas para `ValueLINQRefStruct<T>` y `ValueLINQStruct<T>`.
- **Eager ergonómica (`Func`)**: [ValueLINQErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQErgonomicExtensions.cs), marcada como `[Obsolete]` (JCA0001).
- **Delay con struct selector**: método de instancia de [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs), con una sobrecarga que recibe `scoped ref TSelector` y otra sin parámetros que instancia el selector con `default`.
- **Delay ergonómica (`Func`)**: [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs), también marcada como `[Obsolete]` (JCA0001).

### 1.1 Motor Eager (Alquiler de Búferes)

#### Con Struct Selector
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TResultado> Select<TOrigen, TPredicate, TResultado>(
    this ValueLINQStruct<TOrigen> origen, 
    in TPredicate selector)
    where TPredicate : struct, ISelectDelegado<TOrigen, TResultado>
```
*(Disponible con firma homóloga para `ValueLINQRefStruct<T>`, donde el selector se recibe como `scoped in TPredicate selector`)*

> **Nota — Propagación de arena**: si la consulta de origen pertenece a una arena (creada con `ToValueQuery(origen, arena)` o `ToValueRefQuery(origen, arena)`), el motor Eager de `Select` crea la sesión de resultado **en esa misma arena**, deducida del token de origen (véanse las variantes en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs), líneas 438 y 496). De este modo, todas las sesiones intermedias de la cadena quedan adscritas a la arena y se liberan con su disposición en bloque, incluso cuando la proyección cambia el tipo de elemento (`TOrigen` → `TResultado`). Consulta [Arenas](../Core/Arenas.md) para el detalle del ámbito y la disposición en bloque.

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
public static ValueLINQStruct<TResultado> Select<TOrigen, TResultado>(
    this ValueLINQStruct<TOrigen> origen, 
    Func<TOrigen, TResultado> selector)
```

> **Nota — Diagnóstico JCA0001**: los valores de `JCADiagnostico.JCA0001` (aplicables a las sobrecargas ergonómicas de ambos motores) están definidos en [JCADiagnostico.JCA0001.cs](../../../JCarrillo.AOT.Core/Diagnostico/JCADiagnostico.JCA0001.cs): el `DiagnosticId` es `JCA0001`, el mensaje emitido por el compilador es «Esta firma utiliza delegados Func de entrada y puede generar allocations en el heap. Para evitarlo, asegúrese de usar una expresión lambda estática (static) o los adaptadores basados en struct.» y la URL de ayuda apunta a la wiki de diagnósticos (`UrlBase + "JCA0001.md"`, es decir, <https://github.com/jcarrillo-dev/JCarrillo.AOT.Core/blob/main/docs/Diagnostico/JCA/JCA0001.md>). Estas sobrecargas ergonómicas residen en [ValueLINQErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQErgonomicExtensions.cs) (motor Eager) y [Delay/ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs) (motor diferido), no en `ValueLINQExtensions.cs`.

### 1.2 Motor Lazy/Diferido (Delay)

#### Con Struct Selector (Paso por Referencia)

A diferencia del motor Eager, esta sobrecarga no es un método de extensión de `ValueLINQExtensions.cs`: es un método de **instancia** declarado dentro de `ValueLINQDelayStruct<T, TEnumerator>` (donde `T : allows ref struct` y `TEnumerator : IValueLINQEnumerator<T>, allows ref struct`), en [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs):

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>> Select<TSelector, TResultado>(scoped ref TSelector selector)
    where TSelector : struct, ISelectDelegado<T, TResultado>, allows ref struct
    where TResultado : allows ref struct
```

El método solo tiene dos parámetros genéricos (`TSelector` y `TResultado`); no existe ningún parámetro `TEnumeratorResultado`, ya que el tipo del enumerador resultante es el concreto `ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>`, codificado directamente en el tipo de retorno. El enumerador del pipeline se restringe con `TEnumerator : IValueLINQEnumerator<T>, allows ref struct` en el tipo contenedor, no con `struct, IEnumerator<TOrigen>`.

#### Sin Parámetros (Selector Instanciado por `default`)
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, TSelector, T>> Select<TSelector, TResultado>()
    where TSelector : struct, ISelectDelegado<T, TResultado>, allows ref struct
    where TResultado : allows ref struct
```
Esta sobrecarga instancia internamente el selector con `default(TSelector)`, por lo que está pensada para selectores struct sin estado: evita construir y pasar la instancia manualmente y mantiene las mismas garantías de cero asignaciones e inlining. Sigue el mismo patrón que la sobrecarga de `Where<TPredicate, TState>(TState state)` sin predicado explícito.

#### Sobrecarga Ergonómica (Func)

Definida en [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs):

```csharp
[Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQDelayStruct<TResultado, ValueLINQSelectDelay<TResultado, TEnumerator, ValueLINQFuncSelectSelector<T, TResultado>, T>> Select<T, TEnumerator, TResultado>(
    this ValueLINQDelayStruct<T, TEnumerator> pipeline,
    Func<T, TResultado> selector)
    where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
```

Esta sobrecarga adapta internamente el `Func<T, TResultado>` envolviéndolo en el struct `ValueLINQFuncSelectSelector<T, TResultado>` y delega en el `Select` de instancia de `ValueLINQDelayStruct`, por lo que el tipo del enumerador resultante (`ValueLINQSelectDelay<...>`) queda codificado en el tipo de retorno en lugar de requerir un parámetro genérico `TEnumeratorResultado` explícito.

### 1.3 Fallback de Plataforma (.NET 8.0)
El motor diferido (Delay) requiere la restricción genérica `allows ref struct` de C# 13 / .NET 9.0. Por ello, todo el motor Delay se compila únicamente bajo la directiva `#if NET9_0_OR_GREATER` (véase `ValueLINQ/Delay/ValueLINQDelayStruct.cs` y `Extensiones/ValueLINQ/Delay/ValueLINQDelayExtensions.cs`). Dado que el paquete multi-targetea `net8.0;net9.0;net10.0`, en el target **net8.0 / NativeAOT 8.0** la API Delay simplemente no existe: cualquier intento de usarla produce un error de compilación, no una excepción en tiempo de ejecución. La `PlatformNotSupportedException` que figura en las tablas de benchmarks bajo .NET 8.0 la lanza el propio código del harness de benchmarks (`JCarrillo.AOT.Core.Benchmarks/Extensiones/ValueLINQDelayComparisonBenchmarks.cs`) como marcador de escenario no soportado, no la biblioteca. Las variantes del motor Eager no tienen esta restricción y operan en todos los runtimes.

---

## 2. Abstracción del Selector

Para habilitar la compilación estática e inlining del código, los selectores de struct deben implementar la interfaz [ISelectDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/ISelectDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface ISelectDelegado<TOrigen, TResultado>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
        where TResultado : allows ref struct
#endif
    {
        TResultado Ejecutar(TOrigen objetoLista);
    }
}
```

En .NET 9 o superior, la interfaz declara las restricciones `allows ref struct` sobre `TOrigen` y `TResultado`, lo que permite que el motor Delay proyecte tipos `ref struct` (por ejemplo, `Span<T>`); en .NET 8.0 estas restricciones no existen y aplica el fallback descrito en la sección 1.3.

---

## 3. Ejemplo de Uso Correcto

Implementación de un selector que multiplica cada número por dos:

```csharp
using System.Runtime.CompilerServices;
using JCarrillo.AOT.Core.Extensiones.ValueLINQ;
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

