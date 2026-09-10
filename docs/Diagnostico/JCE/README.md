[Volver al Sitemap de Diagnósticos](../README.md)

# Excepciones JCarrillo (JCE)

Las excepciones bajo el prefijo **JCE** (JCarrillo Exception) son los fallos de **ejecución** del framework `JCarrillo.AOT.Core`. Son el equivalente en tiempo de ejecución del catálogo [JCA](../JCA/README.md), que cubre los diagnósticos de compilación.

Cada excepción del framework añade a su mensaje la dirección de su ficha. Quien la recibe llega así a las causas concretas, a la forma segura de evitarla y, cuando existe, a la escotilla que permite asumir el riesgo de forma explícita.

---

## Por qué existen estas fichas

Un mensaje de excepción tiene que caber en una línea de registro, así que solo puede decir **qué** ocurrió. Estas fichas dicen **por qué** ocurre, que es lo que hace falta para no repetirlo.

Todas siguen el mismo esquema:

1.  **Metadatos**: tipo de excepción, dónde se lanza y qué contrato protege.
2.  **Descripción**: qué situación la dispara y qué garantía se estaba incumpliendo.
3.  **Cómo evitarlo de forma segura**: el rediseño que elimina la causa. Es la respuesta recomendada.
4.  **Cómo asumirlo de forma explícita**: solo cuando existe escotilla en `JCarrillo.AOT.Core.ValueLINQ.Marshalling`, y siempre indicando qué se compromete a garantizar quien la usa.

> [!IMPORTANT]
> La sección 4 no es un atajo para silenciar la excepción. Existe porque hay escenarios en los que el llamante **sí** controla las condiciones que el motor no puede verificar, y negarles la salida los empuja a soluciones peores. Si no puedes enunciar qué garantizas, la respuesta correcta es la sección 3.

---

## Catálogo de Excepciones Disponibles

*   **[JCE0001: Acceso cruzado entre arenas](JCE0001.md)**: una consulta perezosa combina operandos de arenas explícitas distintas.
*   **[JCE0002: Arena inactiva](JCE0002.md)**: se intenta usar o reservar en una arena que fue liberada o nunca se creó.
*   **[JCE0003: Sesión expirada](JCE0003.md)**: se accede a una sesión cuyo búfer ya fue liberado o reutilizado.
*   **[JCE0004: Token de sesión inválido](JCE0004.md)**: se opera sobre una consulta sin inicializar.

---
[Volver al Sitemap de Diagnósticos](../README.md)
