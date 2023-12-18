using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Questions;

namespace Sample.Infra.Repositories.QA;

public class QAContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Question> Questions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var context = GetType();
        modelBuilder.ApplyConfigurationsFromAssembly(context.Assembly, configuration => configuration.Namespace!.Contains(context.Namespace!));
    }
}