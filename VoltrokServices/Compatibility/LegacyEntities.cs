using System;
using System.Collections.Generic;
namespace Voltrok.EF;

public partial class Need
{
    public Guid NeedsId { get; set; }
    public string Code { get; set; } = string.Empty;
}

public partial class PlayerNeed
{
    public Guid PlayerNeedsId { get; set; }
    public Guid PlayersId { get; set; }
    public Guid NeedsId { get; set; }
    public decimal Value { get; set; }
    public Need Needs { get; set; } = new();
}

public partial class ProductsNeedsSupply
{
    public Guid ProductsNeedsSuppliesId { get; set; }
    public Guid ProductsId { get; set; }
    public Guid NeedsId { get; set; }
    public decimal Quantity { get; set; }
    public Need Needs { get; set; } = new();
}

public partial class Product
{
    public Guid ProductsId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int TimeSeconds { get; set; }
    public decimal ProductionCost { get; set; }
    public ICollection<ProductsNeedsSupply> ProductsNeedsSupplies { get; set; } = new List<ProductsNeedsSupply>();
}

public partial class PlayerInventory
{
    public Guid PlayerInventoriesId { get; set; }
    public Guid PlayersId { get; set; }
    public Guid ProductsId { get; set; }
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Product Products { get; set; } = new();
}

public partial class PlayerProductionOrder
{
    public Guid PlayerProductionOrdersId { get; set; }
    public Guid PlayersId { get; set; }
    public Guid ProductsId { get; set; }
    public int Quantity { get; set; }
    public int ProducedQuantity { get; set; }
    public DateTime? CreatedAt { get; set; }
    public Product Products { get; set; } = new();
}

public partial class PlayerProductionProduct
{
    public Guid PlayerProductionProductsId { get; set; }
    public Guid PlayersId { get; set; }
    public Guid ProductsId { get; set; }
}

public partial class MilitaryUnit
{
    public Guid MilitaryUnitsId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int AttackPoints { get; set; }
    public int DefensePoints { get; set; }
    public decimal TravelSpeedKmh { get; set; }
    public int TimeSeconds { get; set; }
    public decimal ProductionCost { get; set; }
}
