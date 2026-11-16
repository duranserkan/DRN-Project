using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Tag;

namespace Sample.Infra.QA.Repositories;

[Scoped<ITagRepository>]
public class TagRepository(QAContext context, IEntityUtils utils) : SourceKnownRepository<QAContext, TagEntity>(context, utils), ITagRepository;
