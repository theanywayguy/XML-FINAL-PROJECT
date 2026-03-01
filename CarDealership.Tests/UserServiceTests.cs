using CarDealershipApi.Services;
using FluentAssertions;
using Xunit;

namespace CarDealershipApi.Tests;

public class UserServiceTests : IDisposable
{
    private readonly string _testFilePath;

    public UserServiceTests()
    {
        // 1. SETUP: This runs BEFORE every single test.
        // We find where the test runner is operating and wipe the XML file.
        // This ensures every test starts with a 100% blank slate.
        _testFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "users.xml");

        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    public void Dispose()
    {
        // 2. TEARDOWN: This runs AFTER every single test.
        // We clean up our mess so we don't leave random XML files on the hard drive.
        if (File.Exists(_testFilePath))
        {
            File.Delete(_testFilePath);
        }
    }

    [Fact]
    public void Constructor_WhenCalled_CreatesAdminUserAutomatically()
    {
        // Arrange & Act
        // Merely instantiating the service triggers the EnsureManagerExists() method
        var service = new UserService();

        // Assert
        var adminUser = service.Authenticate("admin", "ManagerPass123!");

        adminUser.Should().NotBeNull();
        adminUser!.Username.Should().Be("admin");
        adminUser.Role.Should().Be("Manager");
    }

    [Fact]
    public void Authenticate_WithWrongPassword_ReturnsNull()
    {
        // Arrange
        var service = new UserService();

        // Act
        var result = service.Authenticate("admin", "DefinitelyNotThePassword");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Authenticate_WithUnknownUser_ReturnsNull()
    {
        // Arrange
        var service = new UserService();

        // Act
        var result = service.Authenticate("GhostUser", "ManagerPass123!");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void RegisterSalesperson_WithNewUser_SavesAndAuthenticates()
    {
        // Arrange
        var service = new UserService();
        var newUsername = "DwightSchrute";
        var newPassword = "BearsBeetsBattlestarGalactica1!";

        // Act - Register the user
        var registerResult = service.RegisterSalesperson(newUsername, newPassword);

        // Assert - The registration should return true
        registerResult.Should().BeTrue();

        // Act & Assert - We should now be able to log in as that exact user
        var authResult = service.Authenticate(newUsername, newPassword);
        authResult.Should().NotBeNull();
        authResult!.Role.Should().Be("Salesperson");
    }

    [Fact]
    public void RegisterSalesperson_WithDuplicateUsername_ReturnsFalse()
    {
        // Arrange
        var service = new UserService();
        var username = "JimHalpert";

        // Register them the first time
        service.RegisterSalesperson(username, "Password123!");

        // Act - Try to register the exact same username again
        var duplicateRegisterResult = service.RegisterSalesperson(username, "DifferentPassword456!");

        // Assert
        duplicateRegisterResult.Should().BeFalse("because the system should block duplicate usernames");
    }
}