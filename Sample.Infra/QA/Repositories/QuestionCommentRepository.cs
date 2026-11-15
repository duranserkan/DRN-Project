using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.QuestionComment;

namespace Sample.Infra.QA.Repositories;

[Scoped<IQuestionCommentRepository>]
public class QuestionCommentRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, QuestionCommentEntity>(context, utils), IQuestionCommentRepository;
