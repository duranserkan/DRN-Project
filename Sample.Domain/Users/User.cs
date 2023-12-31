using System.ComponentModel.DataAnnotations.Schema;

namespace Sample.Domain.Users;

public class User : AggregateRoot
{
    private User()
    {
    }

    public User(string name, string surname, string userName, ContactDetail contactDetail, Address address)
    {
        Name = name;
        Surname = surname;
        UserName = userName;
        Contact = contactDetail;
        Address = address;
    }

    public string Name { get; private set; } = null!;
    public string Surname { get; private set; } = null!;
    public string UserName { get; private set; } = null!;
    public ContactDetail Contact { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
}

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

    public string Email { get; private set; } = null!;
    public string? Phone { get; private set; }
}

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

    public string Street { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string Postcode { get; private set; } = null!;
    public string Country { get; private set; } = null!;
}