// Tests del gateway: serializar para que el RateLimitedGatewayApiFactory que setea env vars
// bajas (PermitLimit=3, Window=10s) durante su CreateHost no contamine en paralelo a otros
// hosts construidos por GatewayApiFactory. Aceptable: ~30 tests rapidos suman <30s.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
