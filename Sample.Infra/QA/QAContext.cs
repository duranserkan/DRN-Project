using Sample.Domain.QA.Answers;
using Sample.Domain.QA.Categories;
using Sample.Domain.QA.Questions;
using Sample.Domain.QA.Tags;
using Sample.Domain.Users;

namespace Sample.Infra.QA;

public class QAContext : DrnContext<QAContext>
{
    public QAContext(DbContextOptions<QAContext> options) : base(options)
    {
    }

    public QAContext() : base(null)
    {
    }


    public DbSet<User> Users { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Answer> Answers { get; set; }
    public DbSet<QuestionComment> Comments { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Tag> Tags { get; set; }
}