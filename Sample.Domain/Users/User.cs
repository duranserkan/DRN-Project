namespace Sample.Domain.Users;

public class User
{
    public User(string name, string surname, string userName)
    {
        Name = name;
        Surname = surname;
        UserName = userName;
    }

    public int Id { get; private set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string UserName { get; set; }
    public DateTimeOffset DateJoined { get; set; }
    public ContactDetail Contact { get; set; } = null!;
}

public class ContactDetail
{
    public ContactDetail(string email)
    {
        Email = email;
    }

    public Address? Address { get; set; }
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