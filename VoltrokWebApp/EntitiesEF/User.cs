using System;
using System.Collections.Generic;

namespace Voltrok.EF;

public partial class User
{
    public Guid UsersId { get; set; }

    public string Email { get; set; } = null!;

    public string Password { get; set; } = null!;

    public DateTime LastLogin { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Player? Player { get; set; }
}
