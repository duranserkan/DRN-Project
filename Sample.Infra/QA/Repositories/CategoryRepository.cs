using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.SharedKernel.Domain.Repository;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.Categories;

namespace Sample.Infra.QA.Repositories;

[Scoped<ICategoryRepository>]
public class CategoryRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, Category>(context, utils), ICategoryRepository
{
    public override RepositorySettings<Category> Settings { get; set; } = new() { AsNoTracking = true };
}
