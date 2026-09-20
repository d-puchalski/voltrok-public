using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class Product
{
    public Guid ProductsId { get; set; }

    public string Code { get; set; } = null!;

    public int TimeSeconds { get; set; }

    public decimal ProductionCost { get; set; }

    public virtual ICollection<PlayerBattleReportProduct> PlayerBattleReportProducts { get; set; } = new List<PlayerBattleReportProduct>();

    public virtual ICollection<PlayerInventory> PlayerInventories { get; set; } = new List<PlayerInventory>();

    public virtual ICollection<PlayerProductionOrder> PlayerProductionOrders { get; set; } = new List<PlayerProductionOrder>();

    public virtual ICollection<PlayerProductionProduct> PlayerProductionProducts { get; set; } = new List<PlayerProductionProduct>();

    public virtual ICollection<PlayerTransportProduct> PlayerTransportProducts { get; set; } = new List<PlayerTransportProduct>();

    public virtual ICollection<ProductsNeedsSupply> ProductsNeedsSupplies { get; set; } = new List<ProductsNeedsSupply>();

    public virtual ICollection<Trade> Trades { get; set; } = new List<Trade>();
}
