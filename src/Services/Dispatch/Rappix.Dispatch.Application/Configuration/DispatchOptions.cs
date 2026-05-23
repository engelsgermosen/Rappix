namespace Rappix.Dispatch.Application.Configuration;

/// <summary>Opciones del servicio Dispatch.</summary>
public sealed class DispatchOptions
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string SectionName = "Dispatch";

    /// <summary>Parametros del matching geoespacial (GEOSEARCH).</summary>
    public MatchingOptions Matching { get; set; } = new();

    /// <summary>Parametros del matching geoespacial.</summary>
    public sealed class MatchingOptions
    {
        /// <summary>Radio de busqueda en metros (default 5000 = 5km).</summary>
        public double RadiusMeters { get; set; } = 5000d;

        /// <summary>Maximo de candidatos a evaluar por solicitud (default 10).</summary>
        public int CandidateLimit { get; set; } = 10;
    }
}
