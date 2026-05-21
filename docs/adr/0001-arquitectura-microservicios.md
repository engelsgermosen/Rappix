# ADR 001 — Arquitectura de microservicios

## Estado
Aceptado · 2026-05-21

## Contexto

Rappix es una plataforma de delivery multi-vertical (comida, farmacia, supermercado, paquetería) con tres tipos de usuarios (cliente, merchant, courier). Los distintos dominios tienen patrones de carga, requerimientos de latencia y ciclos de evolución diferentes:

- El catálogo es read-heavy y cacheable.
- El tracking en vivo es write-heavy y con SLA de latencia bajo (segundos).
- El dispatching requiere lectura geoespacial intensiva.
- Los pagos requieren consistencia y reintentos robustos.
- Las notificaciones son fire-and-forget masivas.

## Decisión

Adoptamos **arquitectura de microservicios** con los siguientes principios:

1. **Database per service.** Cada microservicio tiene su propia base de datos PostgreSQL (no schema compartido). Esto elimina acoplamiento de datos entre servicios y permite evolucionar esquemas independientemente.

2. **Comunicación predominantemente asíncrona** vía RabbitMQ con MassTransit v8. Cada cambio relevante en un servicio publica un evento de integración que otros pueden consumir.

3. **gRPC para llamadas síncronas críticas** entre servicios (Orders → Pricing). Solo cuando la latencia es crítica y la disponibilidad debe ser fuerte.

4. **Outbox Pattern** para garantizar consistencia entre el commit transaccional y la publicación de eventos. Implementación basada en MassTransit EF Outbox.

5. **API Gateway con YARP** como único punto de entrada desde los frontends Next.js.

6. **MassTransit v8 (open source)** sobre v9 (comercial). v8 está soportado hasta fin de 2026, suficiente para el desarrollo del proyecto. Si se requiere migrar después, OpenTransit es la alternativa open source fork de v8.

## Consecuencias

### Positivas
- Cada servicio puede desplegarse y escalar independientemente.
- Falla aislada: un servicio caído no tumba todo el sistema.
- Tecnologías heterogéneas posibles por servicio (aunque elegimos .NET 10 para todos por consistencia).
- Equipos podrían en el futuro tener ownership de servicios individuales.

### Negativas
- Mayor complejidad operacional (10+ servicios + infraestructura).
- Consistencia eventual entre servicios (no transacciones distribuidas tradicionales).
- Necesidad de Saga Pattern para operaciones que cruzan servicios (pedido = saga).
- Observabilidad distribuida es obligatoria desde día 1.

### Mitigaciones
- Docker Compose para desarrollo local (1 comando levanta todo).
- BuildingBlocks compartido para no reinventar logging, outbox, error handling en cada servicio.
- Serilog + Seq desde el inicio para trazabilidad cross-service vía CorrelationId.
- Migración progresiva a .NET Aspire y observabilidad Pro en fases futuras.
