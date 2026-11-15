using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.QA.User;

namespace Sample.Infra.QA.Configurations;

public class UserConfig : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ComplexProperty(user => user.Contact);
        builder.ComplexProperty(user => user.Address);
    }
}
