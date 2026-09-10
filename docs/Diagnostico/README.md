[Volver al Sitemap de Documentación](../README.md)

# Diagnósticos y Excepciones

Este directorio centraliza la documentación de ingeniería y las guías de remediación de `JCarrillo.AOT.Core`, en dos catálogos que se reparten los dos momentos en que el framework avisa:

*   **En compilación**, mediante reglas de análisis estático y advertencias personalizadas.
*   **En ejecución**, mediante excepciones que remiten desde su propio mensaje a la ficha con las causas y las alternativas.

---

## Estructura de Directorios

Para mantener un diseño modular y limpio, los diagnósticos se agrupan en subcarpetas específicas según su categoría y procedencia. Cada subcarpeta cuenta con su propio **README de índice** que cataloga las reglas pertenecientes a ese namespace:

*   **[JCarrillo Allocation (JCA)](JCA/README.md)**: Reglas de análisis estático propietarias de la librería para optimización de CPU, prevención de allocations en heap y compatibilidad Native AOT. Se emiten en **compilación**.
*   **[JCarrillo Exception (JCE)](JCE/README.md)**: Excepciones de **ejecución** del framework. Cada una añade a su mensaje la dirección de su ficha, donde se explica qué contrato se incumplió, cómo evitarlo de forma segura y, cuando existe escotilla en `Marshalling`, qué se compromete a garantizar quien la usa.

---

## Estructura Común de los Ficheros de Diagnóstico

De forma genérica, todos los archivos de diagnóstico individuales (por ejemplo, `JCAXXXX.md`) implementan la siguiente plantilla unificada:

1.  **Metadatos de la Regla**: Identificador único, severidad predeterminada (Warning/Error) y categoría.
2.  **Descripción**: Explicación conceptual de qué condición ha disparado la regla.
3.  **Causa Técnica**: Explicación a bajo nivel (comportamiento del compilador, asignaciones en montón, coste de CPU) que justifica la alerta.
4.  **Guía de Remediación**: Ejemplos concretos de código incorrecto (con warnings) frente a código corregido (optimizado).
5.  **Configuración**: Métodos para silenciar o personalizar la severidad de la advertencia.

---

## Dirección de Diseño y Roadmap (Analizadores Roslyn)

### Enfoque de Lanzamiento (Obsolete Attributes)
Para la versión actual del framework, el motor de diagnósticos se basa en el atributo nativo de compilación `[Obsolete(..., DiagnosticId = "XXXX")]` de Roslyn.
* **Ventajas**: Solución inmediata y sumamente ligera, con cero coste de mantenimiento y sin impacto en los tiempos de compilación de la solución.
* **Limitaciones**: Al ser metadatos estáticos, el compilador no puede inspeccionar el cuerpo del código cliente (por ejemplo, no distingue si una expresión lambda es estática o capturadora, emitiendo la alerta en ambos casos).

### Evolución de Diseño (Roadmap)
Conforme el ciclo de vida del proyecto avance y se disponga de disponibilidad en el backlog, se prevé la transición hacia un modelo de **analizadores estáticos Roslyn** dedicados y **Source Generators**:

1.  **Roslyn Static Analyzers**: Implementación de analizadores sintácticos que inspeccionen el árbol sintáctico (AST) en tiempo de compilación. Esto permitirá:
    *   Detectar si una lambda es estática (`static`) y **no emitir ninguna alerta** de allocations de forma automática (eliminando la necesidad de supresión manual mediante `#pragma`).
    *   Generar advertencias más inteligentes que distingan el nivel de riesgo real de cada ruta de código.
2.  **Code Fixes Integrados**: Proporcionar refactorizaciones automáticas en el IDE (Quick Fixes) para ayudar al desarrollador a corregir el código en un clic (por ejemplo, añadiendo el modificador `static` o convirtiendo lambdas capturadoras a estructuras).
3.  **Source Generators de Autocorrección**: Investigar la generación de código al vuelo que traduzca las expresiones lambda de los usuarios en structs de ValueLINQ transparentes en tiempo de compilación, eliminando el boilerplate manual sin perder rendimiento.

---
[Volver al Sitemap de Documentación](../README.md)
