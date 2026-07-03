[Volver al Sitemap de Diagnósticos](../README.md)

# Reglas de Diagnóstico JCarrillo Allocation (JCA)

Las advertencias y errores bajo el prefijo **JCA** (JCarrillo Allocation) corresponden a las reglas de optimización de código e integridad de allocations del framework `JCarrillo.AOT.Core`.

Estas reglas se integran en el flujo de compilación mediante analizadores estáticos y atributos de metadatos (`[Obsolete]`), evaluando el código del cliente en busca de patrones que degraden el rendimiento de CPU, incrementen las asignaciones de Heap o pongan en riesgo la compatibilidad nativa AOT.

---

## Ámbito de Diagnóstico (Memory Allocations)

Todos los diagnósticos bajo el código de prefijo **JCA** (representados como `JCAXXXX`) evalúan exclusivamente la ruta caliente del código para detectar y corregir asignaciones implícitas en el Heap (tales como delegados Func, boxing de enumeradores o duplicación evitable de búferes).

---

## ¿Cuándo y por qué se disparan estas alertas?

Estas alertas se disparan automáticamente en tiempo de compilación bajo las siguientes circunstancias:

1.  **Evaluación de APIs Ergonómicas en Rutas Calientes**: Cuando el código de la aplicación invoca extensiones ergonómicas (como lambdas `Func`) en lugar de las firmas nativas estructuradas (`struct`).
2.  **Invocación de Miembros Marcados con Advertencia**: Al compilar, el compilador lee las anotaciones de metadatos asociadas a los operadores de ValueLINQ y genera advertencias (`Warnings`) informativas con el enlace directo a su documentación.
3.  **Compilación en Modo Release**: Es altamente recomendable tratar las advertencias de rendimiento como errores en configuraciones de producción (configurando `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), para garantizar que ningún commit introduzca regresiones de allocations.

---

## Catálogo de Ficheros de Error Disponibles

A continuación se listan las reglas JCA documentadas en este directorio:

*   **[JCA0001: Uso de delegados Func con riesgo de allocations](JCA0001.md)**: Guía sobre cómo resolver alertas de lambdas mediante expresiones estáticas o structs genéricos.
*   **[JCA0002: Materialización a colecciones estándar con allocations](JCA0002.md)**: Guía sobre cómo resolver la alocación en el Heap al convertir a arreglos o listas convencionales.
*   **[JCA0003: Uso de params T[] en .NET 8.0 con allocations](JCA0003.md)**: Explicación de la alocación temporaria inducida por `params` en .NET 8.0 y su mitigación.

---
[Volver al Sitemap de Diagnósticos](../README.md)
