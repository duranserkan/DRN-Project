using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Category;

namespace Sample.Infra.QA.Repositories;

[Scoped<ICategoryRepository>]
public class CategoryRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, CategoryEntity>(context, utils), ICategoryRepository
{
    public override RepositorySettings<CategoryEntity> Settings { get; set; } = new() { AsNoTracking = true };
}
