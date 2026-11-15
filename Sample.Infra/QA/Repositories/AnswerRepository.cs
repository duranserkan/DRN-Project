using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Answer;

namespace Sample.Infra.QA.Repositories;

[Scoped<IAnswerRepository>]
public class AnswerRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, AnswerEntity>(context, utils), IAnswerRepository;
