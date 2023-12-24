using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Questions;
using Sample.Domain.Users;

namespace Sample.Infra.QA;

public class QAContext(DbContextOptions<QAContext> options) : DrnContext<QAContext>(options)
{
    public DbSet<Question> Questions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Category> Categories { get; set; }
}