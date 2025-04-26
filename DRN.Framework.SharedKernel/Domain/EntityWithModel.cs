namespace DRN.Framework.SharedKernel.Domain;

public interface IEntityWithModel<TModel> where TModel : class
{
    TModel Model { get; set; }
}

public abstract class Entity<TModel>(long id = 0) : Entity(id), IEntityWithModel<TModel> where TModel : class
{
    public TModel Model { get; set; } = null!;
}