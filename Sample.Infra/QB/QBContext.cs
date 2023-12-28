using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

namespace Sample.Infra.QB;

//Added to test multiple context support
public class QBContext : DrnContext<QBContext>
{
    public QBContext(DbContextOptions<QBContext> options) : base(options)
    {
    }

    public QBContext() : base(null)
    {
    }
}