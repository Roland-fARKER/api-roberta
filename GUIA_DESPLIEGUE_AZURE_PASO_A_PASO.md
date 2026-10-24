# Guía Práctica de Despliegue Cloud en Microsoft Azure
## API .NET 8, Azure SQL Database, Azure Cosmos DB for MongoDB y CI/CD con GitHub Actions

**Proyecto:** Buffet Vegetariano Tien Lan Zu Shi  
**Stack Tecnológico:** .NET 8 Web API, Azure App Service (Linux), Azure SQL Database, Azure Cosmos DB for MongoDB, GitHub Actions  
**Tipo de Suscripción:** Azure for Students  

---

## 1. Arquitectura del Sistema

El siguiente esquema representa el perímetro de seguridad, el flujo de despliegue continuo y la comunicación interna a través de la red de Azure (*Azure Backbone Network*):

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                       ENTORNO EXTERNO / DESARROLLO                          │
│                                                                             │
│   [ Desarrollador ] ──────> [ Repositorio GitHub ]                          │
│     (Git Push)                (.github/workflows/main_api-roberta.yml)       │
│                                           │                                 │
│                                           ▼ (Publish Profile Automático)    │
└───────────────────────────────────────────┼─────────────────────────────────┘
                                            │
┌───────────────────────────────────────────┼─────────────────────────────────┐
│ AZURE SUBSCRIPTION                        │                                 │
│ RESOURCE GROUP: rg-cloud-project          ▼                                 │
│                               ┌──────────────────────┐                      │
│                               │  Azure App Service   │                      │
│                               │  (.NET 8 Web API)    │                      │
│                               └──────────┬───────────┘                      │
│                                          │                                  │
│             Azure Backbone Network (Tráfico interno de Azure)               │
│             "Allow Azure services and resources to access"                  │
│                                          │                                  │
│                 ┌────────────────────────┴────────────────────────┐         │
│                 ▼                                                 ▼         │
│      ┌────────────────────┐                            ┌───────────────────┐│
│      │ Azure SQL Database │                            │  Azure Cosmos DB  ││
│      │   (TDS - 1433)     │                            │     (MongoDB)     ││
│      └──────────▲─────────┘                            └─────────▲─────────┘│
│                 │                                                │          │
└─────────────────┼────────────────────────────────────────────────┼──────────┘
                  │                                                │
                  └──────────────── Conexión de Gestión ───────────┘
                           (IP Pública residencial en lista blanca)
```

---

## 2. Fase 1: Aprovisionamiento de Infraestructura en Azure Portal

> [!TIP]
> **Ahorro de Créditos de Estudiante:** Configurar todos los recursos en la misma región (`East US` o `Mexico Central`) y usar los niveles **Free (F1)** o **Serverless** para no agotar los créditos de la suscripción educativa.

### 2.1 Crear el Grupo de Recursos
1. En **Azure Portal**, buscar **Resource groups** y hacer clic en **+ Create**.
2. **Subscription:** *Azure for Students*.
3. **Resource group:** `rg-cloud-project`.
4. **Region:** `East US` (o la región más cercana con cuota).
5. Clic en **Review + create** $\rightarrow$ **Create**.

---

### 2.2 Aprovisionar Azure SQL Database
1. Buscar **SQL databases** $\rightarrow$ Clic en **+ Create**.
2. **Resource group:** `rg-cloud-project`.
3. **Database name:** `sqldb-app-prod`.
4. **Server:** Clic en **Create new**:
   * **Server name:** Nombre global único (ej. `sql-buffet-estudiante-2026`).
   * **Location:** Misma región del Resource Group (`East US`).
   * **Authentication method:** **Use SQL authentication**.
   * **Server admin login:** `sqladmin`.
   * **Password:** Contraseña segura (ej. `BuffetAzure2026!`).
5. **Compute + storage:** Seleccionar **Configure database** y elegir **General Purpose - Serverless** (con pausa automática a 1 hora) o nivel **Basic** (5 DTUs).
6. **Redes (Firewall):**
   * **Connectivity method:** *Public endpoint (Redes seleccionadas)*.
   * Marcar **Yes** en *"Allow Azure services and resources to access this server"*.
   * Marcar **Yes** en *"Add current client IP address"* (agrega tu IP local para gestión).
7. Clic en **Review + create** $\rightarrow$ **Create**.

---

### 2.3 Aprovisionar Azure Cosmos DB (API for MongoDB)
1. Buscar **Azure Cosmos DB** $\rightarrow$ Clic en **+ Create**.
2. Seleccionar la tarjeta **Azure Cosmos DB for MongoDB** $\rightarrow$ opción **Request Units (RU) database account**.
3. **Basics:**
   * **Resource group:** `rg-cloud-project`.
   * **Account Name:** Nombre único (ej. `cosmos-buffet-estudiante-2026`).
   * **Location:** Misma región del proyecto.
   * **Capacity mode:** **Serverless** (o *Provisioned Throughput* con *Free Tier* marcado).
   * **Version:** `7.0`.
4. **Redes (Networking):**
   * **Connectivity method:** *Public endpoint*.
   * Marcar: **"Accept connections from within Azure datacenters"** (Aceptar conexiones desde centros de datos de Azure).
   * Agregar tu IP residencial.
5. Clic en **Review + create** $\rightarrow$ **Create**.
6. **Creación de Colección inicial:**
   * Entrar a la cuenta de Cosmos DB $\rightarrow$ **Data Explorer**.
   * Clic en **New Collection**:
     * **Database id:** `SystemLogsDb`
     * **Collection id:** `AuditLogs`
   * Clic en **OK**.
7. En el menú izquierdo ve a **Settings** $\rightarrow$ **Connection strings** y copia el **PRIMARY CONNECTION STRING**.

---

### 2.4 Aprovisionar Azure App Service
1. Buscar **App Services** $\rightarrow$ Clic en **+ Create** $\rightarrow$ **Web App**.
2. **Resource group:** `rg-cloud-project`.
3. **Name:** `api-roberta` (o el nombre único de tu API).
4. **Publish:** *Code*.
5. **Runtime stack:** `.NET 8 (LTS)`.
6. **Operating System:** `Linux`.
7. **Region:** Misma región del Resource Group.
8. **Pricing Plan:** **Free F1** (Costo $0.00).
9. Clic en **Review + create** $\rightarrow$ **Create**.

---

## 3. Fase 2: Configuración de Variables de Entorno en el App Service

En **Azure Portal** $\rightarrow$ Entrar a tu **App Service** $\rightarrow$ **Configuración** (o **Variables de entorno**):

### 3.1 Pestaña: Cadenas de Conexión (Connection Strings)
Hacer clic en **+ Agregar**:

| Nombre | Valor | Tipo |
| :--- | :--- | :--- |
| `DefaultConnection` | `Server=tcp:sql-buffet-estudiante-2026.database.windows.net,1433;Initial Catalog=sqldb-app-prod;Persist Security Info=False;User ID=sqladmin;Password=BuffetAzure2026!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;` | `SQLServer` |
| `MongoDbConnection` | *(Primary Connection String copiada de Cosmos DB)* | `Custom` |

### 3.2 Pestaña: Configuración de la Aplicación (Application Settings)
Hacer clic en **+ Agregar**:

| Nombre | Valor | Descripción |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Permite que Swagger UI esté siempre activo en producción. |
| `MongoLogSettings__DatabaseName` | `SystemLogsDb` | Nombre de la base de datos NoSQL de auditoría. |
| `MongoLogSettings__CollectionName` | `AuditLogs` | Nombre de la colección NoSQL. |

Hacer clic en **Aplicar** *(Apply)* y confirmar para guardar los cambios.

---

## 4. Fase 3: Despliegue de Esquema y Seed en Azure SQL

Dado que la base de datos en la nube está vacía inicialmente, el script SQL se ejecuta directamente desde el portal sin requerir herramientas locales.

### 4.1 Ejecución en Query Editor
1. En **Azure Portal** $\rightarrow$ ir a **SQL databases** $\rightarrow$ seleccionar `sqldb-app-prod`.
2. En el menú izquierdo, ingresar a **Editor de consultas (versión preliminar)** *(Query editor)*.
3. Iniciar sesión con:
   * **Tipo de autenticación:** Autenticación de SQL Server.
   * **Login:** `sqladmin`.
   * **Contraseña:** `BuffetAzure2026!`.
4. Pegar el script de creación de tablas y stored procedures del proyecto.

> [!WARNING]
> **Regla de Azure SQL:** Eliminar cualquier sentencia `CREATE DATABASE` o `USE [nombre_bd];`, ya que en Azure SQL la base de datos ya está creada y no admite el cambio de contexto `USE`.

### 4.2 Seed de Roles y Permisos (Prevención del Error 403 Forbidden)
Para que los endpoints protegidos con roles funcionen, se deben crear los roles y asociar al primer usuario:

```sql
-- 1. Insertar roles requeridos por los controladores
IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleName = 'Administrador')
BEGIN
    INSERT INTO Roles (RoleName, State) VALUES ('Administrador', 1);
END

IF NOT EXISTS (SELECT 1 FROM Roles WHERE RoleName = 'Cajero')
BEGIN
    INSERT INTO Roles (RoleName, State) VALUES ('Cajero', 1);
END

-- 2. Asignar el rol de Administrador al usuario creado (Id = 1)
DECLARE @AdminRoleId INT = (SELECT TOP 1 Id FROM Roles WHERE RoleName = 'Administrador');
DECLARE @UserId INT = 1;

IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = @UserId AND RoleId = @AdminRoleId)
BEGIN
    INSERT INTO UserRoles (UserId, RoleId) VALUES (@UserId, @AdminRoleId);
END
```

---

## 5. Fase 4: Pipeline Automatizado de CI/CD con GitHub Actions

### 5.1 Vinculación con Azure Deployment Center
1. En el App Service $\rightarrow$ **Centro de implementación** *(Deployment Center)*.
2. Seleccionar **GitHub** como origen de código.
3. Autenticar la cuenta y seleccionar:
   * **Organización:** `Roland-fARKER`
   * **Repositorio:** `api-roberta`
   * **Rama:** `main`
4. Azure crea automáticamente el secreto del Publish Profile en GitHub Secrets (`AZUREAPPSERVICE_PUBLISHPROFILE_...`).

### 5.2 Corrección de Rutas en el Workflow (`main_api-roberta.yml`)
Dado que los archivos `.sln` y `.csproj` están alojados dentro de una subcarpeta con espacios (`BUFFET VEGETARIANO TIEN LAN ZU SHI`), se ajustó el archivo [.github/workflows/main_api-roberta.yml](file:///.github/workflows/main_api-roberta.yml) con comillas dobles y despliegue unificado:

```yaml
name: Build and deploy ASP.Net Core app to Azure Web App - api-roberta

on:
  push:
    branches:
      - 'main'
  workflow_dispatch:

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
      - name: Descargar codigo fuente
        uses: actions/checkout@v4

      - name: Set up .NET Core
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restaurar dependencias
        run: dotnet restore "BUFFET VEGETARIANO TIEN LAN ZU SHI/BUFFET VEGETARIANO TIEN LAN ZU SHI.sln"

      - name: Compilar proyecto (Release)
        run: dotnet build "BUFFET VEGETARIANO TIEN LAN ZU SHI/BUFFET VEGETARIANO TIEN LAN ZU SHI.sln" --configuration Release --no-restore

      - name: Publicar artefactos
        run: dotnet publish "BUFFET VEGETARIANO TIEN LAN ZU SHI/BUFFET VEGETARIANO TIEN LAN ZU SHI.csproj" -c Release -o "${{ github.workspace }}/publish" --no-build

      - name: Deploy to Azure Web App
        id: deploy-to-webapp
        uses: azure/webapps-deploy@v3
        with:
          app-name: 'api-roberta'
          slot-name: 'Production'
          package: '${{ github.workspace }}/publish'
          publish-profile: ${{ secrets.AZUREAPPSERVICE_PUBLISHPROFILE_889313995D7D49A89C42DCFA256AE5F0 }}
```

---

## 6. Fase 5: Middleware de Auditoría y Detección Automática de Cosmos DB

Para que la API lea de forma transparente la cadena de conexión de MongoDB sin importar si se declara como App Setting o Connection String, se actualizó la inyección de dependencias en `Program.cs`:

```csharp
// ===== Logs de auditoría en MongoDB =====
builder.Services.Configure<MongoLogSettings>(options =>
{
    builder.Configuration.GetSection("MongoLogSettings").Bind(options);

    // Fallback inteligente: buscar en ConnectionStrings si no está en AppSettings
    if (string.IsNullOrWhiteSpace(options.ConnectionString) || options.ConnectionString.Contains("localhost"))
    {
        var connStr = builder.Configuration.GetConnectionString("MongoDbConnection")
                   ?? builder.Configuration["CUSTOMCONNSTR_MongoDbConnection"];

        if (!string.IsNullOrWhiteSpace(connStr))
        {
            options.ConnectionString = connStr;
        }
    }
});
builder.Services.AddSingleton<ILogService, MongoLogService>();
```

### Sanitización de Contraseñas
El middleware `AuditLoggingMiddleware` intercepta todas las peticiones entrantes y enmascara automáticamente claves sensibles (`password`, `clave`, `token`, etc.) con asteriscos (`******`) antes de almacenar el log en Cosmos DB.

> [!NOTE]
> **Pings de salud de Azure (`/robots933456.txt`):**  
> Cuando el App Service arranca, la infraestructura de Azure envía peticiones automáticas a `/robots<random>.txt` desde la IP `169.254.131.1` para verificar la disponibilidad del contenedor Kestrel. Estas peticiones son interceptadas y registradas legítimamente en la colección NoSQL.

---

## 7. Fase 6: Verificación y Consumo de la API

### 7.1 Acceso a Swagger UI
* **URL Pública:** `https://api-roberta-hgcyaveje3bna8bb.mexicocentral-01.azurewebsites.net/swagger`

### 7.2 Flujo de Prueba Completo
1. **Registro:** Ejecutar `POST /api/Auth/register` creando un usuario.
2. **Login:** Ejecutar `POST /api/Auth/login` enviando las credenciales y copiar el token JWT generado.
3. **Autorización en Swagger:**
   * Hacer clic en el botón verde **Authorize** (arriba a la derecha).
   * Escribir `Bearer <token_jwt>` (con B mayúscula y espacio).
   * Clic en **Authorize** $\rightarrow$ **Close** *(el candado aparecerá cerrado)*.
4. **Consulta a Azure SQL:**
   * Ejecutar `GET /api/Categories` $\rightarrow$ Responde con código **`200 OK`**.
5. **Consulta de Auditoría a Azure Cosmos DB:**
   * Ejecutar `GET /api/Test/logs` $\rightarrow$ Muestra en formato JSON los documentos registrados en tiempo real dentro de la colección `AuditLogs` de Cosmos DB.
