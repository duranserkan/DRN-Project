using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sample.Domain.Users;

namespace Sample.Infra.QA.Configurations;

public class UserConfig : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ComplexProperty(user => user.Contact);
        builder.ComplexProperty(user => user.Address);
    }
}