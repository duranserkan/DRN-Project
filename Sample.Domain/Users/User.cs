namespace Sample.Domain.Users;

public class User
{
    public User(string name)
    {
        Name = name;
    }

    public int Id { get; private set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string UserName {get; set; }
    public DateTimeOffset DateJoined { get; set; }
    public ContactDetail Contact { get; set; } = null!;
}

public class ContactDetail
{
    public Address Address { get; set; } = null!;
    public string? Phone { get; set; }
    public string Email { get; set; }
}

public class Address
{
    public Address(string street, string city, string postcode, string country)
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