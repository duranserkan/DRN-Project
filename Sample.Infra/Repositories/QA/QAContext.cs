using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.Repositories.QA;

public class QAContext(DbContextOptions<QAContext> options) : DrnContext<QAContext>(options)
{
    public DbSet<Question> Questions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }
}