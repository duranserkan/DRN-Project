using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.Repositories.QA;

public class QAContext : DrnContext
{
    public QAContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Question> Questions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }
}