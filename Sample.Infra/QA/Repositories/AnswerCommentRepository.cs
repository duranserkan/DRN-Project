using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.AnswerComment;

namespace Sample.Infra.QA.Repositories;

[Scoped<IAnswerCommentRepository>]
public class AnswerCommentRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, AnswerCommentEntity>(context, utils), IAnswerCommentRepository;
