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

    public string Name { get; set; }
    public string Surname { get; set; }
    public string UserName { get; set; }
    public ContactDetail Contact { get; set; }
    public Address Address { get; set; }
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

    public string Email { get; set; }
    public string? Phone { get; set; }
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

    public string Street { get; set; }
    public string City { get; set; }
    public string Postcode { get; set; }
    public string Country { get; set; }
}