using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Validators;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Validators;

public class CreateBundleValidatorTests
{
    private readonly CreateBundleValidator _validator;

    public CreateBundleValidatorTests()
    {
        _validator = new CreateBundleValidator();
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenAllFieldsAreValid()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "test-bundle",
            Description: "Test description");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenTenantIdIsEmpty()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.Empty,
            Name: "test-bundle");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TenantId");
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenNameIsTooLong()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: new string('a', 201));

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name" && e.ErrorMessage.Contains("200"));
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenDescriptionIsTooLong()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "test-bundle",
            Description: new string('a', 1001));

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description" && e.ErrorMessage.Contains("1000"));
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenDescriptionIsNull()
    {
        // Arrange
        var command = new CreateBundleCommand(
            TenantId: Guid.NewGuid(),
            Name: "test-bundle",
            Description: null);

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
