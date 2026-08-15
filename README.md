# Aura Nova - Backend API

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15.0-blue)

Aura Nova es una plataforma de e-commerce gestionada a través de un panel administrativo exclusivo. Este repositorio contiene **exclusivamente el Backend API**.

## 1. Resumen Ejecutivo
El backend ha completado exitosamente las 12 Fases de desarrollo iterativo. La arquitectura resultante es modular (Basada en Domain-Driven Design ligero), escalable, robusta, securizada contra vectores comunes (XSS, Clickjacking, MIME-sniffing), y completamente dockerizada para su despliegue continuo en **Railway**.

Todas las dependencias están actualizadas y no se han encontrado paquetes vulnerables (`dotnet list package --vulnerable`). Las 133 pruebas unitarias y de integración son exitosas.

## 2. Requisitos y Stack
- **Framework**: .NET 8 SDK
- **Database**: PostgreSQL (Hosteado en Supabase)
- **ORM**: Entity Framework Core 8
- **Authentication**: JWT Bearer Tokens
- **Rate Limiting**: `Microsoft.AspNetCore.RateLimiting`
- **Containerization**: Docker (multi-stage build)
- **Deployment Target**: Railway

## 3. Configuración Local y Secretos

El repositorio no versiona secretos de producción ni conexiones a Supabase reales. Para ejecutar localmente, se recomienda utilizar **User Secrets**:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=aws-0-us-west-2.pooler.supabase.com;Database=postgres;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true" --project src/AuraNova.API
dotnet user-secrets set "JwtSettings:SecretKey" "your_super_secret_key_here" --project src/AuraNova.API
```

Para aplicar las migraciones a una base de datos recién creada:
```bash
dotnet ef database update --project src/AuraNova.Infrastructure --startup-project src/AuraNova.API
```

## 4. Railway Deployment Guide

El backend está diseñado nativamente para desplegarse en [Railway](https://railway.app/).

### Pasos de Despliegue:
1. Crear un nuevo **Empty Service** o conectarlo directamente desde este repositorio de GitHub.
2. Railway detectará automáticamente el `Dockerfile` en la raíz y utilizará `Nixpacks` o el builder de Docker para crear la imagen multi-stage.
3. El `Dockerfile` lee el `PORT` dinámico inyectado por Railway mediante la variable `ASPNETCORE_URLS=http://+:${PORT:-8080}`.
4. **Environment Variables**: En el panel de Railway, configura las siguientes variables obligatorias:

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión de PostgreSQL (Supabase). | `Host=aws-0-us-west-2...` |
| `JwtSettings__SecretKey` | Clave secreta larga para firmar JWTs. | `sUpEr_S3cr3t_...` |
| `JwtSettings__Issuer` | Emisor del token. | `AuraNova.API` |
| `JwtSettings__Audience` | Audiencia del token. | `AuraNova.Admin` |
| `Security__AllowedOrigins__0` | Dominio(s) permitido(s) para CORS en producción. | `https://auranova.pe` |

5. **Migraciones**: Railway no ejecuta migraciones automáticas para evitar corrupción durante deployments concurrentes. Debes ejecutar `dotnet ef database update` localmente apuntando a la base de datos de producción antes de habilitar el tráfico.

## 5. Endpoints Auditados e Inventario (Seguridad)

Todos los endpoints han sido revisados.

| Método | Ruta | Acceso | JWT | Rate Limit Policy |
|--------|------|--------|-----|-------------------|
| GET    | `/api/health` | Público | No | Ninguna |
| GET    | `/api/products` | Público | No | Ninguna |
| GET    | `/api/products/{id}` | Público | No | Ninguna |
| GET    | `/api/delivery-zones` | Público | No | Ninguna |
| GET    | `/api/meeting-points` | Público | No | Ninguna |
| GET    | `/api/business-settings` | Público | No | Ninguna |
| POST   | `/api/orders` | Público | No | `create_order_policy` (3 req/min) |
| GET    | `/api/payment-info` | Público | No | Ninguna |
| POST   | `/api/orders/{id}/accept-quote` | Público | No | `tracking_policy` |
| POST   | `/api/orders/{id}/payment-evidence` | Público | No | `evidence_upload_policy` (3 req/5min) |
| GET    | `/api/public/orders/{code}/tracking/{token}` | Público | No | `tracking_policy` (10 req/min) |
| POST   | `/api/auth/login` | Público | No | `login_policy` (5 req/5min) |
| -- | -- | -- | -- | -- |
| GET,POST,PUT,DELETE | `/api/admin/*` | **Admin** | **Sí** | `admin_policy` (100 req/min) |

> **Nota de Seguridad**: Ningún DTO de los endpoints públicos expone JWTs, contraseñas, ni `TrackingToken` o `ServiceRoleKey`.

## 6. Matriz de Auditoría Administrativa (AdminAuditLogs)

Toda operación que mute el estado del negocio (`POST`, `PUT`, `PATCH`, `DELETE`) en el dashboard es capturada inmutablemente en la base de datos PostgreSQL, incluyendo la IP, User Agent, ID del Administrador (extraído del JWT de forma segura) y Descripción de la acción.

| Acción Auditada | Entidad | Ejemplo de Descripción |
|-----------------|---------|------------------------|
| `LOGIN_SUCCESS` | `Auth` | "User admin@auranova.pe logged in successfully." |
| `CREATE_PRODUCT` | `Product` | "Created new product: Reloj Astral" |
| `UPDATE_PRODUCT_STOCK` | `Product` | "Updated stock for product 1 to 50" |
| `CHANGE_ORDER_STATUS`| `Order` | "Order PED-2026-X changed from WaitingPayment to PaymentReported" |
| `CONFIRM_PAYMENT` | `Payment` | "Confirmed payment for order PED-2026-X" |
| `UPDATE_BUSINESS_SETTINGS`| `BusinessSettings`| "Business settings updated" (No almacena secretos ni URIs privadas). |

## 7. Auditoría de Máquina de Estados (Orders)

Las transiciones de estado de órdenes están blindadas en la lógica de dominio (`AuraNova.Domain.Exceptions.DomainException` arroja RFC 7807 `ProblemDetails` ante transiciones inválidas).

- `WaitingQuote` → `QuoteReady` → `WaitingPayment`
- `WaitingPayment` → `PaymentReported` → `PaymentConfirmed`
- `PaymentConfirmed` → `Preparing` → `Ready`
- `Ready` → `Delivered` o `Shipped`
- `Shipped` → `Delivered`

**Transiciones bloqueadas**:
- `WaitingPayment` → `Delivered` (Imposible)
- `Delivered` → `Cualquier estado` (Imposible)

## 8. Confirmación Final

- **Frontend**: NO se ha implementado ningún código Frontend (React/Next/Angular).
- **Meta API / Yape API / Email**: NO se añadieron canales de comunicación automáticos comerciales (Se mantuvieron los enlaces manuales).
- **Storage**: Se ha asegurado el tamaño de archivos (Max 5MB) y la lista de extensiones (jpg, png, webp).
- **Swagger**: Activado únicamente en entornos de desarrollo (`IsDevelopment()`).
- **Deuda Técnica**: Falta integración real con pasarelas automáticas (Meta WhatsApp API, Email SendGrid) y 2FA/MFA para administradores, las cuales están fuera del alcance de la Fase 12.

**Estado Final**: `READY FOR FRONTEND INTEGRATION`
