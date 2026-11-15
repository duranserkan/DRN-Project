using DRN.Framework.EntityFramework.Domain;
using DRN.Framework.Utils.Entity;
using Sample.Domain.QB;

namespace Sample.Infra.QB.Repositories;

public class QBTestEntityRepository(QBContext context, IEntityUtils utils)
    : SourceKnownRepository<QBContext, QBTestEntity>(context, utils), IQBTestEntityRepository;
