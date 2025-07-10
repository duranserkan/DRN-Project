namespace DRN.Framework.SharedKernel.Domain;

public abstract class AggregateRoot(long id = 0)  : SourceKnownEntity(id);

public abstract class AggregateRoot<TModel>(long id = 0) : SourceKnownEntity<TModel>(id) where TModel : class;