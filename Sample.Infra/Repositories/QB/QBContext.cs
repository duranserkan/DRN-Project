using DRN.Framework.EntityFramework.Context;
using Microsoft.EntityFrameworkCore;

namespace Sample.Infra.Repositories.QB;

//Added to test multiple context support
public class QBContext(DbContextOptions<QBContext> options) : DrnContext<QBContext>(options);