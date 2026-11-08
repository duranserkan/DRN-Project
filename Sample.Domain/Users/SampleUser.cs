using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Sample.Domain.Users;

[SuppressMessage("SonarQube", "S2094", Justification = "AddIdentityCore uses this concrete application user type for its Identity services.")]
public class SampleUser : IdentityUser
{
}
