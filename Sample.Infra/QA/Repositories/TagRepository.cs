using DRN.Framework.EntityFramework;
using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Tags;

namespace Sample.Infra.QA.Repositories;

[Scoped<ITagRepository>]
public class TagRepository(QAContext context, IEntityUtils utils) : SourceKnownRepository<QAContext, Tag>(context, utils), ITagRepository;