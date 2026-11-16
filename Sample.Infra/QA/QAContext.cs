using Sample.Domain.QA.Answer;
using Sample.Domain.QA.Category;
using Sample.Domain.QA.Question;
using Sample.Domain.QA.QuestionComment;
using Sample.Domain.QA.Tag;
using Sample.Domain.QA.User;

namespace Sample.Infra.QA;

[QAContextNpgsqlDbContextOptions]
public class QAContext : DrnContext<QAContext>
{
    public QAContext(DbContextOptions<QAContext> options) : base(options)
    {
    }

    public QAContext() : base(null)
    {
    }

    public DbSet<UserEntity> Users { get; set; }
    public DbSet<QuestionEntity> Questions { get; set; }
    public DbSet<AnswerEntity> Answers { get; set; }
    public DbSet<QuestionCommentEntity> Comments { get; set; }
    public DbSet<CategoryEntity> Categories { get; set; }
    public DbSet<TagEntity> Tags { get; set; }
}
