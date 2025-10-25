using Sample.Domain.Users;

namespace DRN.Test.Tests.Sample.Infra.QA.Repositories.Data;

public static class UserGenerator
{
    public static User New(string prefix, string suffix, ContactDetail? contactDetail = null, Address? address = null)
    {
        contactDetail ??= new ContactDetail($"{prefix}_{suffix}_email");
        address ??= new Address($"{prefix}_{suffix}_street", $"{prefix}_{suffix}_city", $"{prefix}_{suffix}_country", $"{prefix}_{suffix}_postcode");
        
        return new User($"{prefix}_{suffix}_name", $"{prefix}_{suffix}_surname", $"{prefix}_{suffix}_username", contactDetail, address);
    }
}