-- =====================================================================
-- Script de Creación para la Base de Datos de FGate Reverse Proxy
-- Versión: 2.0
-- Descripción: Crea todas las tablas necesarias y añade datos
--              esenciales para el funcionamiento del panel de admin.
-- =====================================================================

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ProxyDB')
BEGIN
    CREATE DATABASE ProxyDB;
END
GO

USE ProxyDB;
GO

-- =====================================================================
-- SECCIÓN 1: CREACIÓN DE TABLAS
-- =====================================================================

-- Tabla para los grupos de endpoints (Clusters en YARP)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EndpointGroups]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EndpointGroups (
        GroupId INT IDENTITY(1,1) PRIMARY KEY,
        GroupName VARCHAR(100) UNIQUE NOT NULL,
        PathPattern VARCHAR(512) NOT NULL,
        MatchOrder INT NOT NULL DEFAULT 0,
        Description NVARCHAR(MAX) NULL,
        ReqToken BIT NOT NULL DEFAULT 1,
        IsInMaintenanceMode BIT NOT NULL DEFAULT 0,
        RateLimitRuleId INT NULL, -- FK a RateLimitRules
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla [EndpointGroups] creada.';
END
GO

-- Tabla para los destinos backend (servicios reales)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BackendDestinations]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.BackendDestinations (
        DestinationId INT IDENTITY(1,1) PRIMARY KEY,
        Address VARCHAR(2048) UNIQUE NOT NULL,
        FriendlyName VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        HealthCheckPath VARCHAR(2048) NULL,
        MetadataJson NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla [BackendDestinations] creada.';
END
GO

-- Tabla de relación entre Grupos y Destinos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EndpointGroupDestinations]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EndpointGroupDestinations (
        EndpointGroupDestinationId INT IDENTITY(1,1) PRIMARY KEY,
        GroupId INT NOT NULL,
        DestinationId INT NOT NULL,
        IsEnabledInGroup BIT NOT NULL DEFAULT 1,
        AssignedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        CONSTRAINT FK_EndpointGroupDestinations_EndpointGroups FOREIGN KEY (GroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT FK_EndpointGroupDestinations_BackendDestinations FOREIGN KEY (DestinationId) REFERENCES dbo.BackendDestinations(DestinationId) ON DELETE CASCADE,
        CONSTRAINT UQ_EndpointGroupDestinations_GroupId_DestinationId UNIQUE (GroupId, DestinationId)
    );
    PRINT 'Tabla [EndpointGroupDestinations] creada.';
END
GO

-- Tabla para los tokens de API
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ApiTokens]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.ApiTokens (
        TokenId INT IDENTITY(1,1) PRIMARY KEY,
        TokenValue VARCHAR(512) UNIQUE NOT NULL,
        Description VARCHAR(255) NULL,
        OwnerName VARCHAR(150) NULL,
        OwnerContact VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        DoesExpire BIT NOT NULL DEFAULT 1,
        ExpiresAt DATETIME2 NULL,
        LastUsedAt DATETIME2 NULL,
        CreatedBy VARCHAR(100) NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla [ApiTokens] creada.';
END
GO

-- Tabla de permisos para los tokens
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TokenPermissions]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.TokenPermissions (
        TokenPermissionId INT IDENTITY(1,1) PRIMARY KEY,
        TokenId INT NOT NULL,
        GroupId INT NOT NULL,
        AllowedHttpMethods VARCHAR(100) NOT NULL DEFAULT 'GET,POST,PUT,DELETE',
        AssignedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        CONSTRAINT FK_TokenPermissions_ApiTokens FOREIGN KEY (TokenId) REFERENCES dbo.ApiTokens(TokenId) ON DELETE CASCADE,
        CONSTRAINT FK_TokenPermissions_EndpointGroups FOREIGN KEY (GroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT UQ_TokenPermissions_TokenId_GroupId UNIQUE (TokenId, GroupId)
    );
    PRINT 'Tabla [TokenPermissions] creada.';
END
GO

-- Tabla para IPs bloqueadas
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[BlockedIPs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.BlockedIPs (
        BlockedIpId INT IDENTITY(1,1) PRIMARY KEY,
        IpAddress VARCHAR(45) UNIQUE NOT NULL,
        Reason NVARCHAR(MAX) NULL,
        BlockedUntil DATETIME2 NULL,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla [BlockedIPs] creada.';
END
GO

-- Tabla para orígenes CORS permitidos
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AllowedCorsOrigins]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.AllowedCorsOrigins (
        OriginId INT IDENTITY(1,1) PRIMARY KEY,
        OriginUrl VARCHAR(512) UNIQUE NOT NULL,
        Description VARCHAR(255) NULL,
        IsEnabled BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UpdatedAt DATETIME2 DEFAULT GETUTCDATE() NOT NULL
    );
    PRINT 'Tabla [AllowedCorsOrigins] creada.';
END
GO

-- Tabla para reglas de Rate Limiting
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RateLimitRules]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.RateLimitRules (
        RuleId INT IDENTITY(1,1) PRIMARY KEY,
        RuleName NVARCHAR(150) NOT NULL,
        PeriodSeconds INT NOT NULL,
        RequestLimit INT NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Tabla [RateLimitRules] creada.';
END
GO

-- Tabla para reglas del WAF
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WafRules]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.WafRules (
        RuleId INT IDENTITY(1,1) PRIMARY KEY,
        RuleName NVARCHAR(150) NOT NULL,
        Description NVARCHAR(500) NULL,
        Pattern NVARCHAR(500) NOT NULL,
        Action NVARCHAR(50) NOT NULL CHECK (Action IN ('Block', 'Log')),
        IsEnabled BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Tabla [WafRules] creada.';
END
GO

-- Tabla de relación entre Grupos y Reglas WAF
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[EndpointGroupWafRules]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.EndpointGroupWafRules (
        EndpointGroupId INT NOT NULL,
        WafRuleId INT NOT NULL,
        PRIMARY KEY (EndpointGroupId, WafRuleId),
        CONSTRAINT FK_EndpointGroupWafRules_EndpointGroups FOREIGN KEY (EndpointGroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT FK_EndpointGroupWafRules_WafRules FOREIGN KEY (WafRuleId) REFERENCES dbo.WafRules(RuleId) ON DELETE CASCADE
    );
    PRINT 'Tabla [EndpointGroupWafRules] creada.';
END
GO

-- Tabla para alertas del sistema
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SystemAlerts]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.SystemAlerts (
        AlertId INT IDENTITY(1,1) PRIMARY KEY,
        TimestampUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Level NVARCHAR(50) NOT NULL,
        Title NVARCHAR(250) NOT NULL,
        Details NVARCHAR(MAX) NULL,
        IsRead BIT NOT NULL DEFAULT 0
    );
    PRINT 'Tabla [SystemAlerts] creada.';
END
GO

-- Tabla para logs de peticiones
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RequestLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.RequestLogs (
        LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
        RequestId VARCHAR(100) UNIQUE NOT NULL,
        TimestampUTC DATETIME2(3) NOT NULL,
        ClientIpAddress VARCHAR(45) NOT NULL,
        HttpMethod VARCHAR(10) NOT NULL,
        RequestPath VARCHAR(2048) NOT NULL,
        QueryString NVARCHAR(MAX) NULL,
        RequestHeaders NVARCHAR(MAX) NULL,
        RequestBodyPreview NVARCHAR(MAX) NULL,
        RequestSizeBytes BIGINT NULL,
        TokenIdUsed INT NULL,
        WasTokenValid BIT NULL,
        EndpointGroupAccessed VARCHAR(100) NULL,
        BackendTargetUrl VARCHAR(2048) NULL,
        ResponseStatusCode INT NOT NULL,
        ResponseHeaders NVARCHAR(MAX) NULL,
        ResponseBodyPreview NVARCHAR(MAX) NULL,
        ResponseSizeBytes BIGINT NULL,
        DurationMs INT NOT NULL,
        ProxyProcessingError NVARCHAR(MAX) NULL,
        UserAgent VARCHAR(512) NULL,
        GeoCountry VARCHAR(100) NULL,
        GeoCity VARCHAR(100) NULL,
        CONSTRAINT FK_RequestLogs_ApiTokens FOREIGN KEY (TokenIdUsed) REFERENCES dbo.ApiTokens(TokenId) ON DELETE SET NULL
    );
    PRINT 'Tabla [RequestLogs] creada.';
END
GO

-- Tabla para logs de auditoría
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AuditLogs]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.AuditLogs (
        AuditId BIGINT IDENTITY(1,1) PRIMARY KEY,
        TimestampUTC DATETIME2 DEFAULT GETUTCDATE() NOT NULL,
        UserId VARCHAR(100) NULL,
        EntityType VARCHAR(100) NOT NULL,
        EntityId VARCHAR(100) NOT NULL,
        Action VARCHAR(255) NOT NULL,
        OldValues NVARCHAR(MAX) NULL,
        NewValues NVARCHAR(MAX) NULL,
        AffectedComponent VARCHAR(100) NULL,
        IpAddress VARCHAR(45) NULL
    );
    PRINT 'Tabla [AuditLogs] creada.';
END
GO

-- Tabla para configuraciones generales del proxy
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProxyConfigurations]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.ProxyConfigurations (
        ConfigurationKey NVARCHAR(100) PRIMARY KEY,
        ConfigurationValue NVARCHAR(500) NOT NULL,
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    PRINT 'Tabla [ProxyConfigurations] creada.';
END
GO

-- Tabla para resúmenes de tráfico por hora
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HourlyTrafficSummary]') AND type in (N'U'))
BEGIN
    CREATE TABLE dbo.HourlyTrafficSummary (
        SummaryId BIGINT IDENTITY(1,1) PRIMARY KEY,
        HourUTC DATETIME2 NOT NULL,
        EndpointGroupId INT NOT NULL,
        HttpMethod VARCHAR(10) NOT NULL,
        RequestCount INT NULL,
        ErrorCount4xx INT NULL,
        ErrorCount5xx INT NULL,
        AverageDurationMs DECIMAL(10,2) NULL,
        P95DurationMs DECIMAL(10,2) NULL,
        TotalRequestBytes BIGINT NULL,
        TotalResponseBytes BIGINT NULL,
        UniqueClientIps INT NULL,
        CONSTRAINT FK_HourlyTrafficSummary_EndpointGroups FOREIGN KEY (EndpointGroupId) REFERENCES dbo.EndpointGroups(GroupId) ON DELETE CASCADE,
        CONSTRAINT UQ_HourlyTraffic_Hour_Group_Method UNIQUE (HourUTC, EndpointGroupId, HttpMethod)
    );
    PRINT 'Tabla [HourlyTrafficSummary] creada.';
END
GO

-- =====================================================================
-- SECCIÓN 2: CONFIGURACIÓN INICIAL (SEEDING)
-- =====================================================================

PRINT 'Iniciando seeding de datos iniciales...';
GO

BEGIN TRANSACTION;
BEGIN TRY

    -- Insertar configuración por defecto para retención de logs
    IF NOT EXISTS (SELECT 1 FROM [dbo].[ProxyConfigurations] WHERE [ConfigurationKey] = 'LogRetentionDays')
    BEGIN
        INSERT INTO [dbo].[ProxyConfigurations] ([ConfigurationKey], [ConfigurationValue])
        VALUES ('LogRetentionDays', '30');
        PRINT 'Configuración inicial [LogRetentionDays] insertada.';
    END

    -- Insertar un origen CORS para desarrollo local (ejemplo)
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AllowedCorsOrigins] WHERE [OriginUrl] = 'http://localhost:3000')
    BEGIN
        INSERT INTO [dbo].[AllowedCorsOrigins] ([OriginUrl], [Description], [IsEnabled])
        VALUES ('http://localhost:3000', 'Frontend de desarrollo local', 1);
        PRINT 'Origen CORS de ejemplo [http://localhost:3000] insertado.';
    END

    COMMIT TRANSACTION;
    PRINT 'Seeding de datos iniciales completado.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: Ocurrió un error durante el seeding de datos iniciales. Se revirtió la transacción.';
    THROW;
END CATCH
GO

-- =====================================================================
-- SECCIÓN 3: DATOS DE EJEMPLO (COMENTADOS)
-- Descomenta y ejecuta esta sección para poblar la base de datos
-- con un ejemplo funcional.
-- =====================================================================
/*
PRINT 'Iniciando seeding de datos de EJEMPLO...';
GO

BEGIN TRANSACTION;
BEGIN TRY
    -- 1. Crear un destino backend de ejemplo
    DECLARE @ExampleDestinationId INT;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[BackendDestinations] WHERE [Address] = 'https://api.example.com')
    BEGIN
        INSERT INTO [dbo].[BackendDestinations] ([Address], [FriendlyName], [IsEnabled], [HealthCheckPath])
        VALUES ('https://api.example.com', 'API de Ejemplo Externa', 1, '/health');
        SET @ExampleDestinationId = SCOPE_IDENTITY();
        PRINT 'Destino de ejemplo [https://api.example.com] creado.';
    END
    ELSE
    BEGIN
        SELECT @ExampleDestinationId = DestinationId FROM [dbo].[BackendDestinations] WHERE [Address] = 'https://api.example.com';
    END

    -- 2. Crear un grupo de endpoints de ejemplo
    DECLARE @ExampleGroupId INT;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[EndpointGroups] WHERE [GroupName] = 'ServiciosPublicos')
    BEGIN
        INSERT INTO [dbo].[EndpointGroups] ([GroupName], [PathPattern], [MatchOrder], [Description], [ReqToken])
        VALUES ('ServiciosPublicos', '/public-api/{**remainder}', 100, 'Endpoints públicos de ejemplo', 1);
        SET @ExampleGroupId = SCOPE_IDENTITY();
        PRINT 'Grupo de ejemplo [ServiciosPublicos] creado.';
    END
    ELSE
    BEGIN
        SELECT @ExampleGroupId = GroupId FROM [dbo].[EndpointGroups] WHERE [GroupName] = 'ServiciosPublicos';
    END

    -- 3. Vincular el destino con el grupo
    IF @ExampleDestinationId IS NOT NULL AND @ExampleGroupId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[EndpointGroupDestinations] WHERE GroupId = @ExampleGroupId AND DestinationId = @ExampleDestinationId)
    BEGIN
        INSERT INTO [dbo].[EndpointGroupDestinations] ([GroupId], [DestinationId])
        VALUES (@ExampleGroupId, @ExampleDestinationId);
        PRINT 'Vinculación entre Grupo [ServiciosPublicos] y Destino [https://api.example.com] creada.';
    END

    -- 4. Crear un token de API de ejemplo
    DECLARE @ExampleTokenId INT;
    IF NOT EXISTS (SELECT 1 FROM [dbo].[ApiTokens] WHERE [Description] = 'Token para Cliente de Ejemplo')
    BEGIN
        INSERT INTO [dbo].[ApiTokens] ([TokenValue], [Description], [OwnerName], [IsEnabled], [DoesExpire])
        VALUES ('EJEMPLO_TOKEN_SEGURO_1234567890', 'Token para Cliente de Ejemplo', 'Cliente Alfa', 1, 0);
        SET @ExampleTokenId = SCOPE_IDENTITY();
        PRINT 'Token de ejemplo [EJEMPLO_TOKEN_SEGURO_1234567890] creado.';
    END
    ELSE
    BEGIN
        SELECT @ExampleTokenId = TokenId FROM [dbo].[ApiTokens] WHERE [Description] = 'Token para Cliente de Ejemplo';
    END

    -- 5. Darle permiso al token para acceder al grupo
    IF @ExampleTokenId IS NOT NULL AND @ExampleGroupId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[TokenPermissions] WHERE TokenId = @ExampleTokenId AND GroupId = @ExampleGroupId)
    BEGIN
        INSERT INTO [dbo].[TokenPermissions] ([TokenId], [GroupId], [AllowedHttpMethods])
        VALUES (@ExampleTokenId, @ExampleGroupId, 'GET,POST');
        PRINT 'Permiso creado para que el token de ejemplo acceda al grupo de servicios públicos.';
    END

    COMMIT TRANSACTION;
    PRINT 'Seeding de datos de EJEMPLO completado.';
END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: Ocurrió un error durante el seeding de datos de EJEMPLO. Se revirtió la transacción.';
    THROW;
END CATCH
GO
*/

PRINT '=====================================================================';
PRINT 'Proceso de base de datos finalizado.';
PRINT '=====================================================================';
GO
