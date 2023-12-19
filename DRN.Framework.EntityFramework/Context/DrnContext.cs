using Microsoft.EntityFrameworkCore;

namespace DRN.Framework.EntityFramework.Context;

public class DrnContext : DbContext
{
    protected DrnContext(DbContextOptions options) : base(options)
    {
    }
}