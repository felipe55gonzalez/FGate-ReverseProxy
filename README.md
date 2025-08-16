# FGate Reverse Proxy

Bienvenido a **FGate**, una puerta de enlace (API Gateway) robusta y configurable, construida con **.NET** y **YARP (Yet Another Reverse Proxy)**. FGate está diseñado para ser el punto de entrada centralizado para gestionar, asegurar y monitorear el acceso a todos tus servicios backend.

---

## 📜 Tabla de Contenidos

1.  [Características Principales](#-características-principales)
2.  [Acceso al Panel de Administración](#-acceso-al-panel-de-administración)
3.  [Configuración Inicial](#️-configuración-inicial)
4.  [Arquitectura y Tecnologías](#-arquitectura-y-tecnologías)
5.  [Casos de Uso](#-casos-de-uso)
6.  [Contribuir](#-contribuir)
7.  [Licencia y Marcas](#-licencia-y-marcas)

---

## 🚀 Características Principales

-   **Panel de Administración Web:** Interfaz gráfica para gestionar toda la configuración del proxy en tiempo real.
-   **Enrutamiento Dinámico:** Configuración de rutas y clústeres gestionada desde la base de datos. No es necesario redesplegar para aplicar cambios.
-   **Autenticación y Autorización Granular:**
    -   Validación de tokens de API.
    -   Permisos basados en grupos de endpoints.
    -   Control de métodos HTTP (GET, POST, etc.) permitidos por token y grupo.
-   **Seguridad Avanzada:**
    -   **Firewall de Aplicaciones Web (WAF):** Define reglas con expresiones regulares para bloquear peticiones maliciosas.
    -   **Límite de Tasa (Rate Limiting):** Protege tus servicios contra peticiones excesivas.
    -   **Bloqueo de Direcciones IP:** Lista negra de IPs gestionada desde el panel.
    -   **Gestión de Orígenes CORS:** Controla qué dominios pueden acceder a tus APIs.
-   **Monitorización y Analíticas:**
    -   Dashboard con estadísticas de tráfico en tiempo real.
    -   Logs detallados de cada solicitud y respuesta.
    -   Análisis de uso por cada token de API.
    -   Sistema de alertas de seguridad y estado.
-   **Herramientas de Desarrollo:**
    -   **Probador de Rutas:** Verifica al instante qué grupo de endpoints manejará una ruta específica.
    -   **Health Checks:** Monitorea el estado de los servicios backend y los deshabilita automáticamente si fallan.

---

## 🔑 Acceso al Panel de Administración

Para acceder a la interfaz web de administración, usa las siguientes credenciales que están definidas en el código:

-   **Usuario:** `admin`
-   **Contraseña:** `ProxyAdmin123!`

> ⚠️ **¡ADVERTENCIA DE SEGURIDAD!**
> Estas credenciales están harcodeadas en `Areas/Admin/Controllers/AccountController.cs`. Es **extremadamente importante** que las cambies antes de desplegar este proyecto en un entorno de producción. Una buena práctica sería mover la gestión de usuarios administradores a la base de datos.

---

## ⚙️ Configuración Inicial

### 1. Base de Datos

-   **Nombre:** `ProxyDB`
-   **Script de creación:** `ProxyDB-CreationQuery.sql` (incluido en el repositorio). Ejecuta este script en tu SQL Server para crear la base de datos y todas las tablas necesarias.

### 2. Configuración de la Aplicación

En `appsettings.json`:

-   **Connection Strings:** Asegúrate de que `ConnectionStrings:ProxyDB` apunta correctamente a tu base de datos.
-   **Kestrel (Puertos):** Define los puertos HTTP/HTTPS en los que escuchará el proxy.

### 3. Datos Iniciales

Para que el proxy funcione, debes configurar al menos un grupo, un destino y la relación entre ellos desde el panel de administración. El script de base de datos incluye una sección de ejemplo comentada para guiarte.

---

## 🏗️ Arquitectura y Tecnologías

-   **Framework:** .NET 8
-   **Proxy Reverso:** YARP (Yet Another Reverse Proxy)
-   **Base de Datos:** SQL Server (configurable vía Entity Framework Core)
-   **ORM:** Entity Framework Core
-   **Frontend (Panel Admin):** ASP.NET Core MVC con Bootstrap y ECharts.js

El núcleo del proyecto utiliza el `IProxyConfigProvider` de YARP para cargar dinámicamente la configuración de rutas y clústeres desde la base de datos. Una serie de `Middlewares` personalizados interceptan las solicitudes para aplicar las políticas de seguridad (IPs, WAF, Rate Limiting, Tokens) antes de que YARP las procese.

---

## 🧪 Casos de Uso

-   **Puerta de Enlace Única:** Una sola URL pública para múltiples microservicios.
-   **Seguridad Centralizada:** Aplica WAF, Rate Limiting, validación de tokens y bloqueo de IPs en un solo lugar.
-   **Balanceo de Carga:** Distribuye el tráfico entre varias instancias de un servicio backend.
-   **Descarga SSL/TLS:** Maneja el cifrado SSL/TLS en el proxy, simplificando los servicios internos.
-   **Logging y Monitorización Central:** Obtén una visión completa del tráfico de todas tus APIs desde un único dashboard.

---

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Puedes:

-   **Reportar errores:** Abre un [issue](https://github.com/felipe5g/FGate-ReverseProxy/issues) describiendo el problema.
-   **Sugerir mejoras:** Propón nuevas ideas en un nuevo *issue*.
-   **Enviar Pull Requests:** Correcciones, mejoras o nuevas funcionalidades son bienvenidas.

---

## 📄 Licencia y Marcas

### Licencia

Este proyecto se distribuye bajo la **Licencia MIT**. Esto te permite usar, modificar y distribuir el software libremente, siempre que incluyas el aviso de copyright original en tu versión. Consulta el archivo `LICENSE` para más información.

### Uso del Nombre "FGate"

El nombre **"FGate"** es la marca que identifica al proyecto original. Puedes usarlo libremente para referirte a este proyecto. Sin embargo, si distribuyes una versión modificada, te pedimos amablemente que lo hagas bajo un nombre diferente para evitar confusiones y distinguir claramente tu versión de la original.