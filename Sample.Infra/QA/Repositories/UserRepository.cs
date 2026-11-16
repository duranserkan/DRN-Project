using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QA.User;

namespace Sample.Infra.QA.Repositories;

[Scoped<IUserRepository>]
public class UserRepository(QAContext context, IEntityUtils utils)
    : SourceKnownRepository<QAContext, UserEntity>(context, utils), IUserRepository;
