# ADR 0002 — Tooling de OpenAPI: generador nativo de .NET 10 + Scalar (en lugar de Swashbuckle)

- **Estado:** Aceptado
- **Fecha:** 2026-05-21
- **Contexto de fase:** Fase 1 (Identity Service)

## Contexto

ASP.NET Core 10 incorpora un generador de documentos OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`,
vía `AddOpenApi`/`MapOpenApi`) que depende de **`Microsoft.OpenApi` 2.0**. Esa librería 2.0 introdujo
cambios de ruptura respecto a la 1.6: tipos movidos del namespace `Microsoft.OpenApi.Models` a
`Microsoft.OpenApi`, eliminación de `OpenApiSecurityScheme.Reference`, nuevos tipos como
`OpenApiSecuritySchemeReference`, etc.

`Microsoft.AspNetCore.OpenApi` es una **dependencia transitiva** de `Rappix.BuildingBlocks.WebApi`
(que todos los servicios referencian), por lo que `Microsoft.OpenApi` 2.0 se carga siempre.

**`Swashbuckle.AspNetCore` 7.2.0** (la versión fijada en Fase 0) está compilado contra
`Microsoft.OpenApi` 1.6. Al coexistir con la 2.0 en el mismo proceso, el solo hecho de tener el
ensamblado de Swashbuckle cargado provoca, al servir `/openapi/v1.json`, un error en tiempo de
ejecución:

```
ReflectionTypeLoadException:
  Method 'GetSwagger' in type 'Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenerator' does not have an implementation.
  Could not load type 'Microsoft.OpenApi.Models.OpenApiDocument' from assembly 'Microsoft.OpenApi, Version=2.0.0.0'.
```

Las versiones de Swashbuckle compatibles con `Microsoft.OpenApi` 2.0 (9.x/10.x) implican un salto
mayor de versión y reescribir la configuración con su nueva API; el ecosistema aún estaba
estabilizándose para .NET 10.

## Decisión

1. **Eliminar `Swashbuckle.AspNetCore` por completo** (de `Directory.Packages.props`,
   `Rappix.BuildingBlocks.WebApi` y `Rappix.Identity.Api`).
2. **Generar el documento OpenAPI con el generador nativo de .NET 10** (`AddOpenApi` + `MapOpenApi`),
   usando *document transformers* para el título/descripción y para el esquema de seguridad Bearer JWT
   (`BearerSecuritySchemeTransformer`, API de `Microsoft.OpenApi` 2.0).
3. **Servir la UI con [Scalar](https://github.com/scalar/scalar)** (`Scalar.AspNetCore`,
   `MapScalarApiReference`), que consume el documento nativo y es compatible con `Microsoft.OpenApi` 2.0.

Rutas resultantes (solo en Development): documento en `/openapi/v1.json`, UI en `/scalar/v1`.

## Consecuencias

- **Positivas:** sin conflicto de versiones; tooling first-party de .NET 10; UI moderna con botón
  *Authorize* para JWT; un patrón único reutilizable por los 10 microservicios.
- **Negativas:** Scalar es una dependencia de terceros (aunque muy alineada con el OpenAPI nativo);
  se pierde la familiaridad de Swagger UI. La generación de comentarios XML de OpenAPI requiere
  habilitar interceptores (`InterceptorsNamespaces`) en el `.csproj` del Api.
- **Alcance:** aplica a todos los servicios; `Rappix.BuildingBlocks.WebApi` ya no arrastra Swashbuckle.
