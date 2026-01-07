using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Enums;
using SignalBeam.Domain.ValueObjects;
using SignalBeam.IdentityManager.Application.Commands;
using SignalBeam.IdentityManager.Application.Repositories;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.IdentityManager.Application.Tests.Commands;

public class RegisterUserHandlerTests
{
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly RegisterUserHandler _handler;

    public RegisterUserHandlerTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _tenantRepository = Substitute.For<ITenantRepository>();
        _subscriptionRepository = Substitute.For<ISubscriptionRepository>();

        _handler = new RegisterUserHandler(
            _userRepository,
            _tenantRepository,
            _subscriptionRepository);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenAllValidationsPass()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "test@example.com",
            Name: "Test User",
            ZitadelUserId: "zitadel-123",
            TenantName: "Test Company",
            TenantSlug: "test-company");

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TenantName.Should().Be(command.TenantName);
        result.Value.TenantSlug.Should().Be(command.TenantSlug);
        result.Value.SubscriptionTier.Should().Be(SubscriptionTier.Free);

        // Verify all entities were created
        await _tenantRepository.Received(1).AddAsync(
            Arg.Is<Tenant>(t => t.Name == command.TenantName && t.Slug == command.TenantSlug),
            Arg.Any<CancellationToken>());

        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Email == command.Email && u.Name == command.Name && u.ZitadelUserId == command.ZitadelUserId),
            Arg.Any<CancellationToken>());

        await _subscriptionRepository.Received(1).AddAsync(
            Arg.Is<Subscription>(s => s.Tier == SubscriptionTier.Free),
            Arg.Any<CancellationToken>());

        // Verify transaction was committed
        await _tenantRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateUserAsAdmin_OnFirstRegistration()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "admin@example.com",
            Name: "Admin User",
            ZitadelUserId: "zitadel-456",
            TenantName: "New Company",
            TenantSlug: "new-company");

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify user was created as Admin
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.Admin),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateTenantWithFreeTier_OnRegistration()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "Regular User",
            ZitadelUserId: "zitadel-789",
            TenantName: "Startup Inc",
            TenantSlug: "startup-inc");

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify tenant was created with Free tier
        await _tenantRepository.Received(1).AddAsync(
            Arg.Is<Tenant>(t => t.SubscriptionTier == SubscriptionTier.Free && t.MaxDevices == 5 && t.DataRetentionDays == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCreateActiveSubscription_OnRegistration()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "User Name",
            ZitadelUserId: "zitadel-999",
            TenantName: "Tech Corp",
            TenantSlug: "tech-corp");

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify subscription was created with correct initial state
        await _subscriptionRepository.Received(1).AddAsync(
            Arg.Is<Subscription>(s =>
                s.Tier == SubscriptionTier.Free &&
                s.Status == SubscriptionStatus.Active &&
                s.DeviceCount == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenUserAlreadyExists()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "existing@example.com",
            Name: "Existing User",
            ZitadelUserId: "existing-zitadel-id",
            TenantName: "Company",
            TenantSlug: "company");

        var existingUser = User.Create(
            UserId.New(),
            TenantId.New(),
            "existing@example.com",
            "Existing User",
            "existing-zitadel-id",
            UserRole.Admin,
            DateTimeOffset.UtcNow);

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("USER_EXISTS");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("already registered");

        // Verify no entities were created
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTenantSlugIsInvalid()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "User Name",
            ZitadelUserId: "zitadel-111",
            TenantName: "Invalid Slug Company",
            TenantSlug: "INVALID SLUG!"); // Invalid: uppercase and spaces

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_SLUG");
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("lowercase letters, numbers, and hyphens");

        // Verify no entities were created
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("A")] // Too short
    [InlineData("a-very-long-slug-that-exceeds-the-maximum-allowed-length-of-64-characters-in-the-system")] // Too long
    [InlineData("-starts-with-hyphen")] // Invalid start (must start with letter or number)
    [InlineData("has space")] // Contains space
    [InlineData("HAS_UPPERCASE")] // Contains uppercase
    [InlineData("has@special")] // Contains special char
    [InlineData("has_underscore")] // Contains underscore
    public async Task Handle_ShouldReturnFailure_WhenTenantSlugFormatIsInvalid(string invalidSlug)
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "User Name",
            ZitadelUserId: "zitadel-222",
            TenantName: "Company",
            TenantSlug: invalidSlug);

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("INVALID_SLUG");
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenTenantSlugAlreadyExists()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "User Name",
            ZitadelUserId: "zitadel-333",
            TenantName: "Another Company",
            TenantSlug: "existing-slug");

        var existingTenant = Tenant.Create(
            TenantId.New(),
            "Existing Company",
            "existing-slug",
            SubscriptionTier.Free,
            DateTimeOffset.UtcNow);

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns(existingTenant);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_SLUG_TAKEN");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("already in use");

        // Verify no entities were created
        await _tenantRepository.DidNotReceive().AddAsync(Arg.Any<Tenant>(), Arg.Any<CancellationToken>());
        await _userRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("test-company")]
    [InlineData("my-startup-2024")]
    [InlineData("acme-corp")]
    [InlineData("ab")] // Minimum valid length
    [InlineData("a0")] // Starts with letter, contains number
    public async Task Handle_ShouldAccept_ValidTenantSlugs(string validSlug)
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "user@example.com",
            Name: "User Name",
            ZitadelUserId: $"zitadel-{validSlug}",
            TenantName: "Company",
            TenantSlug: validSlug);

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TenantSlug.Should().Be(validSlug);
    }

    [Fact]
    public async Task Handle_ShouldLinkUserToTenant_Correctly()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "linked@example.com",
            Name: "Linked User",
            ZitadelUserId: "zitadel-444",
            TenantName: "Linked Company",
            TenantSlug: "linked-company");

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        _tenantRepository.GetBySlugAsync(command.TenantSlug, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        TenantId? capturedTenantId = null;

        await _tenantRepository.AddAsync(Arg.Do<Tenant>(t => capturedTenantId = t.Id), Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify user was created with the same tenant ID
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(u => u.TenantId == capturedTenantId),
            Arg.Any<CancellationToken>());

        // Verify subscription was created with the same tenant ID
        await _subscriptionRepository.Received(1).AddAsync(
            Arg.Is<Subscription>(s => s.TenantId == capturedTenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRespectCancellationToken()
    {
        // Arrange
        var command = new RegisterUserCommand(
            Email: "cancel@example.com",
            Name: "Cancel User",
            ZitadelUserId: "zitadel-555",
            TenantName: "Cancel Company",
            TenantSlug: "cancel-company");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _userRepository.GetByZitadelIdAsync(command.ZitadelUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<User?>(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            async () => await _handler.Handle(command, cts.Token));
    }
}
