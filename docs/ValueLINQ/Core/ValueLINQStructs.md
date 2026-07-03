[Volver al Core de ValueLINQ](README.md) | [Volver a ValueLINQ](../README.md)

# Componentes Core: ValueLINQStruct y ValueLINQRefStruct

El pipeline de ValueLINQ expone dos contenedores estructurados fundamentales para representar consultas activas: `ValueLINQStruct<T>` y `ValueLINQRefStruct<T>`. Aunque comparten firmas semánticas y lógica de procesamiento casi idénticas, difieren radicalmente en sus restricciones de compilación y garantías de asignación de memoria.

---

## 1. Tabla Comparativa: record struct vs ref struct

| Característica | `ValueLINQStruct<T>` | `ValueLINQRefStruct<T>` |
| :--- | :--- | :--- |
| **Tipo C#** | `record struct` | `ref struct` |
| **Ubicación en Memoria** | Pila (Stack) o Heap (si es embebido/boxeado) | Estrictamente en Pila (Stack) |
| **Implementación de Interfaces** | Sí (`IDisposable`) | No (prohibido por especificación del lenguaje para ref structs) |
| **Riesgo de Boxing** | Sí (si se asigna a `object` o `IDisposable`) | **Imposible** (garantizado por el compilador) |
| **Almacenamiento en Campos** | Permitido en cualquier clase o estructura | Solo permitido en otros `ref struct` |
| **Uso en Métodos Asíncronos** | Permitido libremente entre llamadas `await` | **Prohibido** (no puede cruzar fronteras de `await`) |
| **Patrón de Liberación (using)** | Interfaz estándar `IDisposable` | Duck-Typing (patrón síncrono `Dispose()`) |

---

## 2. Detalles de Implementación

### `ValueLINQStruct<T>` (ver [ValueLINQStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/ValueLINQStruct.cs))
Diseñado para la máxima flexibilidad. Es adecuado cuando la consulta debe ser retornada desde un método que no puede usar `ref struct`, o cuando necesita cruzar operaciones síncronas complejas. Al ser un `record struct`, se beneficia de semánticas de valor y comparaciones automáticas. 

*Advertencia: Al ser un struct convencional, el programador debe ser cuidadoso de no castearlo a interfaces o pasarlo a métodos que fuercen su promoción al heap (boxing), lo cual anularía el beneficio de cero allocations.*

### `ValueLINQRefStruct<T>` (ver [ValueLINQRefStruct.cs](../../../JCarrillo.AOT.Core/ValueLINQ/ValueLINQRefStruct.cs))
Diseñado para el máximo rendimiento y escenarios críticos de Native AOT. Al estar declarado como `ref struct`, el compilador de C# impone restricciones estáticas insalvables: no puede ser boxeado, no puede escapar del stack frame actual, no puede ser asignado como campo de una clase ordinaria y no puede utilizarse dentro de máquinas de estado asíncronas (`async/await`). 

Esto proporciona una **garantía de hierro en tiempo de compilación** de que la consulta jamás alocará un solo byte de memoria en el heap ni causará recolecciones de basura.

---

## 3. Seguridad de las APIs de Población (Diseño Internal)

Tanto en `ValueLINQStruct<T>` como en `ValueLINQRefStruct<T>`, los métodos encargados de poblar o modificar la sesión de consulta (como `Añadir(T valor)` y `Añadir(ReadOnlySpan<T> span)`) están declarados con acceso **`internal`** en lugar de `public`.

Esta es una decisión de diseño crítica por las siguientes razones de seguridad:

1.  **Protección de la Inmutabilidad del Pipeline**: Las consultas ValueLINQ representan pipelines fluidos que se crean a partir de un origen de datos contiguo y se consumen de inmediato. Permitir que código externo añada elementos de forma ad-hoc destruiría la predictibilidad del flujo de datos.
2.  **Prevención de Excepciones de Expiración**: La adición manual de elementos fuera del flujo síncrono del pipeline incrementa el tiempo de vida de la sesión en la pila. Si el código del usuario retiene la consulta abierta interactuando con ella manualmente, es muy probable que supere el límite de inactividad de **5 minutos (medido)** del StateManager, provocando que el temporizador de fondo expire el slot y lance excepciones de sesión expirada al intentar reutilizar el token.
3.  **Evitación de Fugas de Memoria (Memory Leaks)**: Si el método `Añadir` fuera público, los desarrolladores podrían verse tentados a instanciar y poblar manualmente estructuras de consulta sin la debida protección de un bloque `using`. Dado que estas estructuras reservar buffers del pool global, cualquier omisión de `Dispose()` provocaría que el buffer quedara huérfano hasta la recolección periódica del StateManager, degradando el rendimiento del pool.
4.  **Evasión de Costes de Sincronización**: Las llamadas a `Añadir` requieren adquirir un lock local sobre el slot de la tabla fija del StateManager para asegurar espacio y copiar datos. El framework encapsula estas llamadas en operaciones optimizadas en bloque (como la población a partir de Spans). Exponer `Añadir` al público invitaría a inserciones unitarias en bucles del usuario, resultando en un overhead severo de sincronización ($O(N)$ locks).

---

## 4. Ciclo de Vida y Duck-Typing mediante using var

Para garantizar la devolución determinista del buffer de datos al `ArrayPool<T>`, ValueLINQ requiere liberar la consulta mediante el patrón `using`.

### El Patrón en `ValueLINQStruct<T>`
Al implementar `IDisposable`, la estructura se acopla de forma natural al compilador:
```csharp
using (var query = datos.ToValueQuery())
{
    // Procesamiento
} // Aquí se invoca query.Dispose() de forma atómica y segura
```

### El Patrón en `ValueLINQRefStruct<T>` (Duck-Typing)
Dado que un `ref struct` no puede implementar la interfaz `IDisposable`, ValueLINQ aprovecha la característica de **Duck-Typing** provista por el compilador de C#. El compilador no exige la interfaz; únicamente requiere la existencia de un método que cumpla con el siguiente patrón físico exacto:
*   Debe ser público.
*   Debe llamarse `Dispose`.
*   No debe recibir argumentos.
*   Debe retornar `void`.

En `ValueLINQRefStruct.cs`:
```csharp
public void Dispose()
    => ValueLINQStateManager<T>.LiberarMetadatos(_token);
```

Gracias a esto, el desarrollador puede escribir exactamente el mismo bloque de control:
```csharp
using var query = datos.ToValueRefQuery();
// Procesamiento...
// Al salir del alcance (scope), el compilador inserta una llamada directa a query.Dispose()
```
Este enfoque elimina la necesidad de despachos virtuales a través de la interfaz `IDisposable`, logrando una llamada directa y estática altamente eficiente que devuelve los recursos al StateManager de forma atómica.

## 5. El Motor Lazy: ValueLINQDelayStruct<T, TEnumerator>

El motor diferido de ValueLINQ introduce la estructura `ValueLINQDelayStruct<T, TEnumerator>`, la cual representa una consulta diferida (lazy) síncrona.

### Características y Diferencias de Diseño
- **Tipo C#**: Está declarada como un `ref struct`.
- **Gestión en Pila**: A diferencia de `ValueLINQStruct<T>` y `ValueLINQRefStruct<T>`, esta estructura **no alquila buffers** del pool de memoria (`ArrayPool<T>`) ni se registra en el `ValueLINQStateManager<T>` durante su inicialización.
- **Ciclo de Vida de Cero Asignaciones**: Todos los operadores intermedios (como `Where` y `Select`) se resuelven en la pila en tiempo de iteración. El procesamiento de datos se realiza elemento a elemento directamente sobre la colección original durante el recorrido del bucle `foreach`. Al no requerir almacenamiento temporal, su perfil de asignación en el heap es de **0 B (medido)** y no requiere invocar `Dispose()`.
- **Requisitos del Compilador**: Depende de características de C# 13 y .NET 9.0 o superior, en particular de la restricción genérica `allows ref struct`, que permite que las interfaces genéricas y delegados manejen tipos por valor en la pila. En entornos .NET 8.0 o NativeAOT 8.0, las APIs correspondientes lanzan una excepción `PlatformNotSupportedException` **(medido)** de forma limpia.

---
[Volver al Core de ValueLINQ](README.md)

