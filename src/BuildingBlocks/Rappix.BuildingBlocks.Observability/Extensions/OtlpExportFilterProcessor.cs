using System.Diagnostics;
using OpenTelemetry;

namespace Rappix.BuildingBlocks.Observability.Extensions;

/// <summary>
/// Descarta los spans gRPC client de exportacion OTLP (p. ej. LogsService/Export del sink OTLP de
/// Serilog): telemetria sobre telemetria. La instrumentacion gRPC no expone filtro, asi que se quita
/// el flag Recorded en OnEnd y el processor del exporter (registrado despues) lo ignora.
/// </summary>
internal sealed class OtlpExportFilterProcessor : BaseProcessor<Activity>
{
    private const string OtlpCollectorServicePrefix = "opentelemetry.proto.collector.";

    public override void OnEnd(Activity data)
    {
        // Semconv RPC nueva: "rpc.method" lleva el nombre completo ("<paquete>.<Servicio>/<Metodo>");
        // la anterior separaba "rpc.service". Se aceptan ambas.
        if (IsOtlpCollectorCall(data.GetTagItem("rpc.method")) || IsOtlpCollectorCall(data.GetTagItem("rpc.service")))
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }

    private static bool IsOtlpCollectorCall(object? tag) =>
        tag is string value && value.StartsWith(OtlpCollectorServicePrefix, StringComparison.Ordinal);
}
