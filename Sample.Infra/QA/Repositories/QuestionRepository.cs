using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Question;

namespace Sample.Infra.QA.Repositories;

[Scoped<IQuestionRepository>]
public class QuestionRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, QuestionEntity>(context, utils), IQuestionRepository;
