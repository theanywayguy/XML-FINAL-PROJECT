using CarDealershipApi.Models;
using System.Xml.Linq;

namespace CarDealershipApi.Services;

public class UserService
{
    private readonly string _filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "users.xml");
    private List<User> _users = new();

    public UserService()
    {
        EnsureDataDirectoryExists();
        LoadUsers();
        EnsureManagerExists();
    }

    private void EnsureDataDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void LoadUsers()
    {
        if (!File.Exists(_filePath))
            return;

        var doc = XDocument.Load(_filePath);

        if (doc.Root != null)
        {
            _users = doc.Root.Elements("user")
                .Select(x => new User
                {
                    Id = x.Element("id")?.Value ?? Guid.NewGuid().ToString(),
                    Username = x.Element("username")?.Value ?? "",
                    PasswordHash = x.Element("passwordHash")?.Value ?? "",
                    Role = x.Element("role")?.Value ?? ""
                })
                .ToList();
        }
    }

    private void EnsureManagerExists()
    {
        if (!_users.Any(u => u.Role == "Manager"))
        {
            var manager = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ManagerPass123!"),
                Role = "Manager"
            };

            _users.Add(manager);
            SaveUsers();
        }
    }

    private void SaveUsers()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            new XElement("users",
                _users.Select(u => new XElement("user",
                    new XElement("id", u.Id),
                    new XElement("username", u.Username),
                    new XElement("passwordHash", u.PasswordHash),
                    new XElement("role", u.Role)
                ))
            )
        );

        doc.Save(_filePath);
    }

    public User? Authenticate(string username, string password)
    {
        var user = _users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

        // BCrypt.Verify automatically handles the salt check against the stored hash
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }
        return user;
    }

    public bool RegisterSalesperson(string username, string password)
    {
        if (_users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            return false; // User already exists
        }

        _users.Add(new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Salesperson"
        });

        // Persist the changes to the XML file immediately
        SaveUsers();

        return true;
    }
}