using SignalBeam.DeviceManager.Application.Commands;
using SignalBeam.DeviceManager.Application.Validators;

namespace SignalBeam.DeviceManager.Tests.Unit.Validators;

public class RegisterDeviceValidatorTests
{
    private readonly RegisterDeviceValidator _validator;

    public RegisterDeviceValidatorTests()
    {
        _validator = new RegisterDeviceValidator();
    }

    [Fact]
    public void Validate_ShouldPass_WhenCommandIsValid()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: "Test Device",
            Metadata: "{\"location\":\"lab\"}");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenTenantIdIsEmpty()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.Empty,
            DeviceId: Guid.NewGuid(),
            Name: "Test Device");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TenantId");
    }

    [Fact]
    public void Validate_ShouldFail_WhenDeviceIdIsEmpty()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.Empty,
            Name: "Test Device");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeviceId");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_ShouldFail_WhenNameIsInvalid(string? name)
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: name!);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_ShouldPass_WhenNameIsAtMaxLength()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: new string('A', 200));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        // Arrange
        var command = new RegisterDeviceCommand(
            TenantId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            Name: new string('A', 201));

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }
}
