using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class PlayerBattleReportProduct
{
    public Guid PlayerBattleReportProductsId { get; set; }

    public Guid PlayerBattleReportsId { get; set; }

    public Guid ProductsId { get; set; }

    public int Quantity { get; set; }

    public virtual PlayerBattleReport PlayerBattleReports { get; set; } = null!;

    public virtual Product Products { get; set; } = null!;
}
