# BuildingBlocks

Cuatro librerías compartidas que evitan reinventar la rueda en cada uno de los 10 microservicios.

## `Rappix.BuildingBlocks.Core`

Tipos de dominio compartidos. **Cero dependencias** externas (solo .NET base).

| Componente | Propósito |
|---|---|
| `Result` / `Result<T>` | Resultado tipado de operación (éxito o error) — evita excepciones como flujo de control |
| `Error` | Error con código, descripción y tipo (Validation, NotFound, Conflict, etc) |
| `Entity<TId>` | Clase base de entidades DDD con identidad |
| `AggregateRoot<TId>` | Raíz de agregado que acumula eventos de dominio |
| `IDomainEvent` | Eventos que ocurren dentro del proceso (no cruzan servicios) |
| `IDateTimeProvider` | Abstracción del reloj para tests determinísticos |
| `PagedRequest` / `PagedResult<T>` | Paginación estándar de endpoints de listado |

## `Rappix.BuildingBlocks.Messaging`

Integración con MassTransit + RabbitMQ y soporte del patrón Outbox.

| Componente | Propósito |
|---|---|
| `IIntegrationEvent` | Marca eventos que viajan por RabbitMQ entre servicios |
| `IntegrationEvent` | Base record con `EventId` y `OccurredOnUtc` |
| `OutboxMessage` | Entidad persistida que garantiza publicación tras commit |
| `MessagingExtensions.AddRappixMessaging()` | Registra MassTransit con RabbitMQ con reintentos exponenciales |

**Uso típico en un servicio:**
```csharp
builder.Services.AddRappixMessaging(
    builder.Configuration,
    serviceName: "orders",
    configure: cfg =>
    {
        cfg.AddConsumer<OrderCreatedConsumer>();
    });
```

## `Rappix.BuildingBlocks.Observability`

Configuración estandarizada de Serilog → Seq con enrichers.

| Componente | Propósito |
|---|---|
| `UseRappixLogging(serviceName)` | Bootstrap de Serilog con consola + Seq + enrichers |
| `UseRappixRequestLogging()` | Middleware de request logging con UserId, UserAgent, CorrelationId |

**Uso típico en `Program.cs`:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseRappixLogging("orders");
// ...
var app = builder.Build();
app.UseRappixRequestLogging();
```

## `Rappix.BuildingBlocks.WebApi`

Componentes web reutilizables.

| Componente | Propósito |
|---|---|
| `ResultExtensions.ToHttpResult()` | Convierte `Result<T>` en `IResult` HTTP con ProblemDetails (RFC 7807) |
| `GlobalExceptionHandler` | Handler de excepciones no manejadas → ProblemDetails 500 |
| `CorrelationIdMiddleware` | Lee/genera `X-Correlation-Id` y enriquece logs |
| `IdempotencyMiddleware` | Detecta `Idempotency-Key` y previene reprocesar la misma operación |

**Uso típico en `Program.cs`:**
```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// ...
app.UseExceptionHandler();
app.UseCorrelationId();
app.UseIdempotency();  // solo en servicios con operaciones mutating sensibles
```

## Regla de oro

Los BuildingBlocks **nunca** contienen lógica de negocio. Solo plataforma y plumbing. Si encuentras código de negocio aquí, refactorízalo al servicio que corresponde.
