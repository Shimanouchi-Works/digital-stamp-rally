using System;
using System.Collections.Generic;

namespace MeisterArchives.Database;

public partial class PriceSetting
{
    public int Id { get; set; }

    public int? MaxYen { get; set; }

    public int? MinYen { get; set; }

    public int? SaleFeeYen { get; set; }

    public double? SaleFeeParcent { get; set; }

    public int? SaleFeeYenMin { get; set; }

    public double? PaymentFeePercent { get; set; }

    public int? PaymentFeeYen { get; set; }

    public virtual ICollection<Archive> Archives { get; set; } = new List<Archive>();
}
