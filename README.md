# UNIVERSIDAD MARIANO GALVEZ DE GUATEMALA
### Curso: Análisis de Sistemas II

---

# SigesEdu - Sistema de Gestión Educativa (Prototipo V1)

SigesEdu es una plataforma web institucional orientada a la gestión académica, financiera y administrativa. Este repositorio contiene el Prototipo V1, cuyo objetivo principal es materializar el diseño estructurado del sistema, validando la arquitectura base, la segregación de responsabilidades y la viabilidad técnica de los requerimientos analizados.

## 📋 Metodología y Análisis de Sistemas (RUP)

El ciclo de vida del proyecto se rige estrictamente bajo la metodología **Rational Unified Process (RUP)**, garantizando un enfoque iterativo, incremental y centrado en la arquitectura.

*   **Fase de Inicio:** Definición del alcance del sistema, modelado del negocio e identificación de actores principales (Administrador, Coordinador, Docente, Tesorero, Estudiante, Padre).
*   **Fase de Elaboración:** Mitigación de riesgos técnicos, estructuración de la arquitectura N-Capas y desarrollo de la base de datos transaccional.
*   **Artefactos Generados:** Todo el desarrollo está respaldado por documentación técnica estructurada, incluyendo casos de uso, diagramas de clases, diagramas de secuencia y diagramas de estado (elaborados con PlantUML y draw.io), reflejando la trazabilidad desde los requerimientos hasta el código fuente.

## 🏗️ Arquitectura de Software

El sistema implementa el patrón de Arquitectura N-Capas (N-Tier) aplicando principios de *Clean Architecture* para garantizar alta cohesión y bajo acoplamiento:

1.  **SigesEdu.Domain (Dominio):** Contiene el núcleo del negocio. Define las entidades principales, enums, interfaces de repositorios (Contratos) y las excepciones lógicas del dominio. No tiene dependencias externas.
2.  **SigesEdu.Application (Aplicación):** Contiene los casos de uso del sistema. Implementa los servicios (ej. `AuditoriaService`, `CalificacionesAppService`) y define los DTOs para la transferencia de datos.
3.  **SigesEdu.Infrastructure (Infraestructura):** Maneja la persistencia y servicios externos. Incluye las implementaciones de repositorios, inyección de SQLite, el motor de seguridad criptográfica y la generación de reportes físicos.
4.  **SigesEdu.UI (Presentación):** Capa ASP.NET Core MVC que gestiona el enrutamiento, controladores, interceptores de sesión (Filtros RBAC) y las vistas.

## 💻 Stack Tecnológico y Persistencia

*   **Backend:** C# 12 / .NET 10 (ASP.NET Core MVC).
*   **Persistencia:** SQLite 3. Configurado explícitamente en modo **WAL (Write-Ahead Logging)** para soportar alta concurrencia y lectura transaccional segura.
*   **Seguridad Criptográfica:** Hash de contraseñas mediante algoritmo PBKDF2 (HMAC-SHA256) con generación de `Salt` dinámico (128 bits).
*   **Generación de Documentos:** QuestPDF.
*   **Frontend UI:** HTML5, CSS3, JavaScript, Bootstrap 5.

## 🚀 Funcionalidades Desarrolladas (V1)

*   **Panel Administrativo ROOT:** Consola centralizada para la supervisión y mantenimiento del sistema.
*   **Control de Accesos Basado en Roles (RBAC):** Restricción de rutas y operaciones según el nivel de privilegios del usuario activo.
*   **Bitácora de Auditoría Inmutable:** Registro transaccional automatizado (`IAuditoriaService`) que captura acciones críticas, módulo afectado, marca de tiempo y dirección IP del ejecutor para garantizar la trazabilidad.
*   **Buscador Transaccional:** Motor de búsqueda integrado para la filtración de credenciales y roles.

## ⚙️ Instrucciones de Despliegue Local

> **Nota de Confidencialidad:** Los archivos físicos de la base de datos (`.db`, `-wal`, `-shm`) y el directorio de almacenamiento de legajos (`storage/`) están excluidos del control de versiones mediante `.gitignore` para proteger la integridad de los datos de prueba y documentos de identidad.

Para compilar y ejecutar el proyecto:

1. Clonar el repositorio localmente.
2. Abrir la solución `SigesEdu.slnx` (Visual Studio 2022 o compatible).
3. Localizar el script de migración inicial en `SigesEdu.Infrastructure/Persistence/Migrations/001_InitialSchema.sql`.
4. Ejecutar el script en un motor de bases de datos compatible (ej. DB Browser for SQLite) para generar el esquema estructural.
5. Guardar el archivo generado como `sigesedu.db` dentro del directorio raíz del proyecto de interfaz (`SigesEdu.UI`).
6. Compilar y ejecutar la aplicación.
