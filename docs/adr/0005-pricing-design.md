# ADR 0005 — Diseño del Pricing Service (cotización persistida, orden de cálculo, surge, cupones)

- **Estado:** Aceptado
- **Fecha:** 2026-05-22
- **Contexto de fase:** Fase 4 (Pricing Service)

## Contexto

El Pricing Service calcula el precio total de un pedido y emite una **cotización** (Quote) con su desglose
completo. Lo invocará Orders (fase futura) durante el checkout. Concentra lógica de negocio densa con
precisión decimal, varias estrategias componibles (surge, envío, descuentos) y cupones con concurrencia.
Reaplica la plantilla de Catalog/Merchants (CQRS, Result, outbox, doble puerto gRPC) y reutiliza los
building blocks. Este ADR registra las decisiones no triviales.

## Decisiones

### 1. Cotización PERSISTIDA con expiración, no cálculo stateless

`Quote` es un **agregado raíz persistido** (líneas + desglose embebido + estado `Active|Consumed|Expired`)
con `ExpiresAtUtc` (10 min desde la creación, configurable). El cliente arma el carrito, recibe un
`quoteId` + breakdown + `expiresAtUtc`; Orders referenciará ese `quoteId` y, al consumirlo, se marca
`Consumed`.

- **Por qué persistida:** el precio mostrado al cliente debe ser el precio cobrado. Una cotización efímera
  permitiría que el precio cambie entre que el cliente lo ve y crea el pedido (surge, cupón agotado). Al
  persistir y expirar, el precio queda **congelado y auditable** durante una ventana acotada.
- **Expiración perezosa:** al leer una cotización vencida (`IsExpiredAt`) se marca `Expired` y se devuelve
  `Pricing.Quote.Expired`. El barrido batch (Hangfire o background service) queda como **hook futuro
  documentado**; en Fase 4 la validación perezosa en lectura es suficiente y evita un job innecesario.
- **No se borran:** las cotizaciones expiran, no se eliminan (auditoría e historial de precios).

### 2. Orden de cálculo determinístico (decisión de negocio)

El orden afecta el total, así que es una decisión de negocio explícita y fija, implementada en el
`QuoteCalculator` (función **pura y estática**, sin I/O) y cubierta por pruebas exhaustivas:

```
subtotal      = Σ (precio + modificadores) × cantidad
surge         = subtotal × multiplicador            (adicional = subtotal × (mult − 1))
descuento     = Σ directivas sobre el subtotal con surge, topado a ese subtotal
serviceFee    = % configurable sobre el subtotal base
deliveryFee   = base por vertical + distancia × tarifa/km
tax (ITBIS)   = % sobre (bienes netos + fees gravables)   — la propina NUNCA es gravable
total         = bienes netos + envío + servicio + impuesto + propina
```

- **Descuento sobre el subtotal con surge**, sumando las directivas aplicables y **topando** el total al
  propio subtotal (nunca genera bienes netos negativos).
- **Service fee sobre el subtotal base** (no el surgeado): es la comisión de plataforma al cliente sobre el
  valor de los bienes.

### 3. Política de redondeo: una sola vez, al final, bancario

Todo el dinero es `decimal(19,4)` (convención global de EF: `HavePrecision(19,4)`). El cálculo corre en
**precisión completa** y cada componente se redondea **una sola vez al final** a la unidad menor (2
decimales) con **redondeo bancario** (`MidpointRounding.ToEven`). El **total es la suma de los componentes
ya redondeados**, de modo que el desglose mostrado siempre cuadra (transparencia). Redondear al final (no
en cada paso) evita acumulación de error. Ni un `float`/`double` en cálculos de dinero (el analizador no lo
atrapa; se revisó a mano).

### 4. Surge dinámico por zona + horario × demand factor (hoy stand-in de Dispatch)

La demanda real (pedidos activos vs couriers libres) vivirá en Orders/Dispatch, que **aún no existen**. En
Fase 4 el surge es dinámico por **zona geográfica + franja horaria** (reglas `SurgeRule` editables por
admin, en tabla, no hardcode) multiplicado por un **`demand_factor` configurable** (`PricingOptions`,
default 1.0).

- **El `demand_factor` es un stand-in** de la señal de demanda que proveerá Dispatch en una fase futura.
  Hoy se lee de configuración (no del request del cliente: evita que el cliente manipule su propio surge).
  Cuando exista Dispatch, esa señal alimentará el factor sin cambiar el contrato.
- **Cap de seguridad:** el multiplicador final nunca supera `MaxSurgeMultiplier` (x3.0, configurable). Es un
  hard limit aplicado tras multiplicar base × demanda.
- Las franjas se evalúan en **hora local** (offset configurable; RD = AST = −4) sobre la hora UTC.

### 5. Service fee = % de plataforma configurable, no la comisión del merchant

El **service fee** que paga el cliente es un **porcentaje de plataforma configurable** sobre el subtotal
(`PricingOptions.ServiceFeePercentage`), distinto de la **comisión del merchant** (lo que la plataforma le
descuenta al comercio de su liquidación, que no forma parte de lo que paga el cliente). El servicio gRPC de
Merchants (`MerchantValidationService`) **no expone** la comisión —vive solo en su superficie REST de
admin—, así que Pricing **no la consume**. Integrar una comisión por-merchant que module el service fee
requeriría extender el contrato gRPC de Merchants y queda como **mejora futura documentada**.

### 6. Cupones: uso redimido al consumir (no al cotizar), con concurrencia xmin

- **`Coupon`** (agregado): código único, `DiscountType` (porcentaje/fijo), `Value`, `MaxUses`, `UsedCount`,
  `MinOrderAmount`, vigencia, `PerUserLimit`, `MerchantId` opcional (global o de un merchant), borrado
  lógico (query filter).
- **El uso se incrementa al CONSUMIR la cotización, no al cotizar**: evita quemar usos en cotizaciones
  abandonadas. `UsedCount` usa el token **`xmin`** de PostgreSQL como concurrencia optimista: dos consumos
  concurrentes que compartan cupón chocan y uno reintenta, de modo que **`MaxUses` no se excede** (probado
  con 10 consumos concurrentes sobre `MaxUses=5` → exactamente 5 redenciones, igual que la no-sobreventa de
  stock en Catalog).
- **Límite por usuario:** se registra una `CouponRedemption` por (cupón, cliente, cotización) al consumir;
  al cotizar se cuentan las redenciones del usuario para validar `PerUserLimit`. Es *best-effort* en la
  cotización (varias cotizaciones abiertas con el mismo cupón pasan la validación), pero `MaxUses` + `xmin`
  acotan el uso global de forma estricta.
- **Un cupón por cotización** (decisión simple). El descuento de primera compra se acumula con el cupón solo
  si `AllowStackingCouponWithFirstOrder` lo permite (default true); si no, prevalece el cupón.
- Validación tipada al cotizar (`Pricing.Coupon.*`): existe, activo, vigente, no excede `MaxUses`, cumple
  monto mínimo, dentro del límite por usuario y aplica al merchant. Un cupón inválido **falla la cotización**
  (no se ignora en silencio).

### 7. `ConsumeQuote` es la operación que el plan llamó `MarkCouponUsed`

El plan preveía un gRPC `MarkCouponUsed(quoteId)`. Se implementa como **`ConsumeQuote(quoteId)`** porque
**consumir la cotización es exactamente lo que dispara el uso del cupón**: marca la cotización `Consumed` y,
si tenía cupón, lo redime en la **misma transacción**. Un `MarkCouponUsed` separado del consumo arriesgaría
doble conteo o estados inconsistentes. El nombre `ConsumeQuote` es más preciso y cumple el mismo rol.

### 8. Strategy pattern para surge, envío y descuentos; orquestador determinístico

`ISurgeStrategy`, `IDeliveryFeeStrategy` (por vertical, con resolutor + fallback estándar) e `IDiscountRule`
(primera compra + cupón) se registran en DI y el handler `CreateQuote` los **compone en orden
determinístico**, resuelve los precios y delega la aritmética en el `QuoteCalculator` puro. Esto mantiene el
cálculo testeable y extensible (agregar una regla de descuento o una tarifa por vertical no toca el motor).

### 9. Dos clientes gRPC con resiliencia y fallback; servidor gRPC propio

- **A Catalog** (`GetItemPricing`): precio + comprabilidad de cada item. `AddStandardResilienceHandler`
  (retry + circuit breaker); si Catalog no responde, **fallback al `ItemPriceCache` local** (alimentado por
  `ItemCreatedIntegrationEvent`). El cache es una optimización/respaldo; la fuente de verdad del precio es
  Catalog.
- **A Merchants** (`GetMerchantBasicInfo`): valida merchant activo + vertical. Si no responde, se **procede
  sin bloquear** la cotización (fallback documentado: no tumbar el checkout por un fallo transitorio).
- **Servidor propio** `PricingService` (`QuotePrice`, `ConsumeQuote`) en el puerto 8081 (h2c), mismo cálculo
  que el REST; los montos viajan como **string** para preservar la precisión decimal. Doble puerto Kestrel
  (8080 REST / 8081 gRPC), idempotencia excluida del puerto gRPC (ADR-0003).

## Consecuencias

- **Positivas:** precio congelado y auditable por ventana; cálculo determinístico, transparente y
  exhaustivamente testeado; cupones sin exceso de uso bajo concurrencia; lecturas entre servicios
  resilientes; motor de pricing extensible por estrategias; cero acoplamiento del dominio a EF/gRPC.
- **Negativas / costes:** el límite por usuario es best-effort al cotizar; el `demand_factor` es hoy
  configuración (no demanda real); el service fee no refleja la comisión por-merchant; sin barrido batch de
  expiración (perezoso en lectura).
- **TODO futuro:** conectar `demand_factor` a la señal real de Dispatch; barrido batch de cotizaciones
  expiradas (Hangfire); `ItemPriceChangedIntegrationEvent` de Catalog para refrescar el cache; exponer la
  comisión del merchant por gRPC si se decide modular el service fee por comercio.
