# ADR 0010 — Diseño del Notifications Service (canal conmutable Fake/SendGrid, proyecciones locales, idempotencia por clave de negocio)

- **Estado:** Aceptado
- **Fecha:** 2026-05-23
- **Contexto de fase:** Fase 9 (Notifications Service) — última pieza del backend de Rappix

> Este ADR es un STUB del commit 1. Se completa al final de la fase (commit 12) con la decisión final y los detalles de implementación verificados. Los criterios de aceptación están en el plan de Fase 9.

## Contexto (resumen)

Con Payments (Fase 8) cerrado, el flujo completo del pedido emite todos sus eventos terminales reales pero ninguno notifica al usuario. Fase 9 introduce **Rappix.Notifications**, un servicio que consume los eventos del pedido (Orders/Dispatch/Payments) y notifica por email a los tres actores (cliente, merchant, courier) a través de un canal abstracto conmutable.

## Decisiones (a documentar al cierre)

1. Abstracción `INotificationChannel` con `FakeNotificationChannel` (default) + `SendGridNotificationChannel` (opt-in) — mismo patrón que `IPaymentGateway` Fake/Stripe.
2. Resolución de emails vía **3 proyecciones locales** (`UserContact` desde Identity, `MerchantContact` desde Merchants, `NotificationOrder` desde Orders/Dispatch) — sin gRPC síncrono, sin modificar contratos existentes.
3. Idempotencia en 3 niveles: inbox EF (MessageId) + unique index por clave de NEGOCIO `(RelatedOrderId, RecipientUserId, NotificationType)` + side-effect guard transaccional.
4. Plantillas de email texto plano hardcoded en C# (HTML como follow-up).

## Follow-ups (post-Fase 9)

- HTML email templates.
- Push/SMS channels (Firebase, Twilio).
- Preferencias de notificación por usuario.
- Backfill script de proyecciones (cold-start de `MerchantContact`).
- Reintento automático de notificaciones fallidas (Quartz job).
- SendGrid webhook para bounces/spam reports.
