using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Catalog.Domain.Items;

/// <summary>Opcion de un grupo de modificadores (p. ej. "Grande" +50). Entidad hija del agregado <see cref="Item"/>.</summary>
public sealed class ModifierOption : Entity<Guid>
{
    private ModifierOption()
    {
    }

    internal ModifierOption(Guid id, Guid modifierId, string name, decimal priceDelta)
        : base(id)
    {
        ModifierId = modifierId;
        Name = name;
        PriceDelta = priceDelta;
    }

    /// <summary>Grupo de modificadores al que pertenece.</summary>
    public Guid ModifierId { get; private set; }

    /// <summary>Nombre de la opcion.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Ajuste de precio que aplica esta opcion (puede ser negativo para descuentos).</summary>
    public decimal PriceDelta { get; private set; }
}
