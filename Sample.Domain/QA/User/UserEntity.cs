using Microsoft.EntityFrameworkCore;
using DRN.Framework.SharedKernel.Attributes;
using Sample.Contract.QA.Users;

namespace Sample.Domain.QA.User;

[SampleEntityType(SampleEntityTypes.User)]
public class UserEntity : AggregateRoot
{
    private UserEntity()
    {
    }

    public UserEntity(string name, string surname, string userName, ContactDetail contactDetail, Address address)
    {
        Name = name;
        Surname = surname;
        UserName = userName;
        Contact = contactDetail;
        Address = address;
    }

    [Personal] public string Name { get; private set; } = null!;
    [Personal] public string Surname { get; private set; } = null!;
    [Personal] public string UserName { get; private set; } = null!;
    public ContactDetail Contact { get; private set; } = null!;
    public Address Address { get; private set; } = null!;

    public UserDto ToDto() => new(this);
}

[Owned]
public class ContactDetail
{
    private ContactDetail()
    {
    }

    public ContactDetail(string email, string? phone = null)
    {
        Email = email;
        Phone = phone;
    }

    [Personal] public string Email { get; private set; } = null!;
    [Personal] public string? Phone { get; private set; }
}

[Owned]
public class Address
{
    private Address()
    {
    }

    public Address(string street, string city, string country, string postcode)
    {
        Street = street;
        City = city;
        Postcode = postcode;
        Country = country;
    }

    [Personal] public string Street { get; private set; } = null!;
    [Personal] public string City { get; private set; } = null!;
    [Personal] public string Postcode { get; private set; } = null!;
    [Personal] public string Country { get; private set; } = null!;
}
