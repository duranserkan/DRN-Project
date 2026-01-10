using DRN.Framework.EntityFramework.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DRN.Framework.EntityFramework.Context.DataProtection;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class DrnDataProtectionContextOptionsAttribute() : NpgsqlPerformanceSettingsAttribute(multiplexing: false)
{
    public override bool UsePrototypeMode { get; set; } = false;
    public override bool UsePrototypeModeWhenMigrationExists { get; set; } = false;

    public override void ConfigureDbContextOptions<TContext>(DbContextOptionsBuilder builder, IServiceProvider? serviceProvider)
    {
        base.ConfigureDbContextOptions<TContext>(builder, serviceProvider);
        builder.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NoEntityTypeConfigurationsWarning));
    }
}