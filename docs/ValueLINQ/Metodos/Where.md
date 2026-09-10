[Volver a Métodos y Extensiones](README.md) | [Volver a ValueLINQ](../README.md)

# Operador Where

El operador `Where` filtra los elementos de una colección basándose en un predicado estructurado de tipo `struct`. A diferencia del operador `Where` de LINQ estándar, que recibe un delegado `Func<T, bool>` (provocando asignaciones en el heap e impidiendo el inlining del JIT/AOT), ValueLINQ utiliza un parámetro genérico de tipo struct que implementa la interfaz `IWhereDelegado<TOrigen, TDato>`.

---

## 1. Firmas del Operador

El operador `Where` está repartido en varios archivos según el motor y el estilo de invocación:

- **Eager con struct predicado**: métodos de extensión en [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs), con sobrecargas para `ValueLINQRefStruct<TOrigen>` y `ValueLINQStruct<TOrigen>`.
- **Eager ergonómico (`Func<T, bool>`)**: métodos de extensión en [ValueLINQErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQErgonomicExtensions.cs).
- **Delay con struct predicado**: método de instancia del propio `ref struct` en [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs).
- **Delay ergonómico (`Func<T, bool>`)**: método de extensión en [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs).

Las sobrecargas son las siguientes:

### 1.1 Motor Eager (Alquiler de Búferes)

#### Con Struct Predicado
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQStruct<TOrigen> Where<TOrigen, TDato, TPredicate>(
    this ValueLINQStruct<TOrigen> origen, 
    TDato dato, 
    in TPredicate predicado)
    where TPredicate : struct, IWhereDelegado<TOrigen, TDato>
```
*(Disponible una variante equivalente para `ValueLINQRefStruct<T>`, cuyo predicado se declara como `scoped in TPredicate predicado`)*

#### Sobrecarga Ergonómica (Func)
```csharp
[Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
public static ValueLINQStruct<TOrigen> Where<TOrigen>(
    this ValueLINQStruct<TOrigen> origen, 
    Func<TOrigen, bool> predicado)
```
Estas sobrecargas ergonómicas residen en [ValueLINQErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQErgonomicExtensions.cs) (eager) y en [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs) (delay), no en ValueLINQExtensions.cs. El diagnóstico referenciado por el atributo se define en [JCADiagnostico.JCA0001.cs](../../../JCarrillo.AOT.Core/Diagnostico/JCADiagnostico.JCA0001.cs): `Id` = "JCA0001"; `Mensaje` = "Esta firma utiliza delegados Func de entrada y puede generar allocations en el heap. Para evitarlo, asegúrese de usar una expresión lambda estática (static) o los adaptadores basados en struct."; `Url` = enlace a la página de ayuda del diagnóstico (`UrlBase + "JCA0001.md"`).

### 1.2 Motor Lazy/Diferido (Delay)

#### Con Struct Predicado (Paso por Referencia)
El `Where` diferido con struct predicado no es un método de extensión estático, sino un método de instancia de `ValueLINQDelayStruct<T, TEnumerator>` definido en [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs) (líneas 28-34):

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>> Where<TPredicate, TState>(
    TState state,
    scoped ref TPredicate predicate)
    where TPredicate : struct, IWhereDelegado<T, TState>, allows ref struct
    where TState : allows ref struct
```

El tipo de retorno no preserva `TEnumerator`: cada operador compone el tipo del enumerador en el propio tipo genérico devuelto (aquí `ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>`, un combinador perezoso definido en [ValueLINQWhereDelay.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQWhereDelay.cs)), de modo que la canalización completa queda codificada estáticamente en el tipo. La restricción del enumerador declarada en `ValueLINQDelayStruct<T, TEnumerator>` es `TEnumerator : IValueLINQEnumerator<T>, allows ref struct`.

#### Con Predicado por Defecto (Instanciación Implícita)
```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, TPredicate, TState>> Where<TPredicate, TState>(TState state)
    where TPredicate : struct, IWhereDelegado<T, TState>, allows ref struct
    where TState : allows ref struct
```
Esta sobrecarga crea el predicado internamente con `default(TPredicate)`, por lo que no es necesario declarar ni pasar la instancia por referencia. Está pensada para predicados sin estado propio (structs vacíos). Como el tipo del predicado no puede inferirse del argumento, debe indicarse explícitamente en los parámetros genéricos, por ejemplo: `.Where<FiltroPares, int>(2)`. Definida en [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs) (líneas 36-43).

#### Sobrecarga Ergonómica (Func)
Reside en [ValueLINQDelayErgonomicExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/ValueLINQDelayErgonomicExtensions.cs) (clase estática `ValueLINQDelayErgonomicExtensions`), no en ValueLINQExtensions.cs:

```csharp
[Obsolete(JCADiagnostico.JCA0001.Mensaje, DiagnosticId = JCADiagnostico.JCA0001.Id, UrlFormat = JCADiagnostico.JCA0001.Url)]
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ValueLINQDelayStruct<T, ValueLINQWhereDelay<T, TEnumerator, ValueLINQFuncWherePredicate<T>, Func<T, bool>>> Where<T, TEnumerator>(
    this ValueLINQDelayStruct<T, TEnumerator> pipeline,
    Func<T, bool> predicate)
    where TEnumerator : IValueLINQEnumerator<T>, allows ref struct
```

Obsérvese que: (1) el tipo de retorno no es el mismo `ValueLINQDelayStruct<T, TEnumerator>` de origen, sino un nuevo pipeline cuyo enumerador es `ValueLINQWhereDelay<...>` que envuelve al anterior (composición de tipos en tiempo de compilación); (2) la restricción del enumerador es `IValueLINQEnumerator<T>` con `allows ref struct` (C# 13); y (3) internamente adapta el `Func<T, bool>` mediante el struct [ValueLINQFuncWherePredicate.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delegados/ValueLINQFuncWherePredicate.cs) y delega en el método de instancia `Where` de `ValueLINQDelayStruct`.

### 1.3 Requisito de Plataforma (.NET 9+)
El motor diferido (Delay) requiere la restricción genérica `allows ref struct` de C# 13 / .NET 9. En los TFM inferiores a .NET 9 (net8.0), toda la API Delay se excluye de la compilación mediante `#if NET9_0_OR_GREATER` (véase [ValueLINQDelayStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Delay/ValueLINQDelayStruct.cs) y [Extensiones/ValueLINQ/Delay/](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/Delay/)), por lo que la biblioteca no expone estos operadores en .NET 8: cualquier intento de usarlos produce un error en tiempo de compilación, no una excepción en tiempo de ejecución. Las entradas `PlatformNotSupportedException` (PNSE) que aparecen en las tablas de benchmarks bajo **.NET 8.0 / NativeAOT 8.0** las lanza un stub del propio proyecto de benchmarks ([ValueLINQDelayComparisonBenchmarks.cs](../../../JCarrillo.AOT.Core.Benchmarks/Extensiones/ValueLINQDelayComparisonBenchmarks.cs)), no la biblioteca. Las variantes del motor Eager no tienen esta restricción y operan en todos los runtimes.

---

## 2. Abstracción del Predicado

Para habilitar la compilación estática e inlining del código, los predicados de struct deben implementar la interfaz [IWhereDelegado.cs](../../../JCarrillo.AOT.Core/ValueLINQ/Interfaces/IWhereDelegado.cs):

```csharp
namespace JCarrillo.AOT.Core.ValueLINQ.Interfaces
{
    public interface IWhereDelegado<TOrigen, TDato>
#if NET9_0_OR_GREATER
        where TOrigen : allows ref struct
        where TDato : allows ref struct
#endif
    {
        bool Ejecutar(TOrigen objetoLista, TDato otro);
    }
}
```

En compilaciones para .NET 9 o superior, las restricciones `allows ref struct` permiten que tanto el elemento (`TOrigen`) como el dato de comparación (`TDato`) sean `ref struct`, requisito del motor diferido (Delay) descrito en la sección 1.3; en .NET 8.0 estas restricciones se excluyen mediante compilación condicional.

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

## 4. Semántica de Propiedad y Arenas

El motor eager de `Where` **consume la consulta de origen**: la libera siempre en un bloque `finally` (`origen.Dispose()`, [ValueLINQExtensions.cs](../../../JCarrillo.AOT.Core/Extensiones/ValueLINQ/ValueLINQExtensions.cs), líneas 336 y 400), tanto si la operación tiene éxito como si lanza una excepción. Por tanto, la consulta de origen no debe reutilizarse ni disponerse manualmente después de la llamada, y en un pipeline encadenado solo la consulta final necesita `using`/`Dispose` (como muestra el ejemplo de la sección 3).

La consulta resultado se crea **en la misma arena que el origen**: el token de arena de 64 bits (`origen.TokenArena`) se propaga directamente a la nueva sesión mediante el constructor `new ValueLINQStruct<T>(origen.TokenArena, capacidad)` (o `ValueLINQRefStruct<T>`), de modo que una cadena iniciada con `ToValueQuery(..., arena)` o `ToValueRefQuery(..., arena)` mantiene todas sus sesiones intermedias ligadas al ámbito y a la generación de esa arena; la disposición en bloque de la arena las alcanza todas, y las consultas creadas sin arena explícita operan en la arena ambiente 0.

Véase [Arenas.md](../Core/Arenas.md) para el ciclo de vida completo de las arenas y la propagación en los operadores eager.

---

## 5. Análisis de Rendimiento (Mediciones)

Los datos corresponden a una prueba combinada de filtrado y proyección (`Where` + `Select`) con un tamaño de muestra $N = 1000$ (medido) ejecutada en el harness centralizado.

*   **Harness**: BenchmarkDotNet v0.15.8 con `MemoryDiagnoser` y `ThreadingDiagnoser`.
*   **Suite**: `JCarrillo.AOT.Core.Benchmarks.ValueLINQ.Secuencias.SecuenciaWhereSelectBenchmarks`.

### Tabla Comparativa de Rendimiento (Size = 1000)

| Runtime / Entorno | Método | Latencia Media (Mean) | Heap Allocated | Ratio | Notas |
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

---

## 6. Interpretación de los Datos

1. **Eficiencia en Pila y Cero Asignaciones**: Tanto `ValueLINQStruct` como `ValueLINQRefStruct` con delegados estructurados (`IWhereDelegado`, `ISelectDelegado`) registran **0 B (medido)** asignados en heap en todos los runtimes probados (.NET 8, 9, 10 y NativeAOT), frente a los 104 B / 144 B del LINQ estándar de la BCL.
2. **Ventaja de CPU con Delegados Struct**: En .NET 10.0 JIT, `ValueLINQRefStructWhereSelectDelegados` alcanza **1,275.3 ns (medido)** frente a los 2,114.2 ns del LINQ estándar (un factor de aceleración de 1.66x / ratio 0.60x), eliminando la barrera de inlining.
3. **Escenario Native AOT**: En NativeAOT 10.0, el coste de despacho dinámico en el baseline LINQ eleva su latencia a 12,635.2 ns **(medido)**. Las variantes con delegados operan entre 1,447.0 ns y 1,490.1 ns **(medido)** con **0 B (medido)** asignados, resultando hasta **8.7 veces más rápidas** debido al inlining estático completo soportado por el compilador AOT.

---
[Volver a Métodos y Extensiones](README.md)


