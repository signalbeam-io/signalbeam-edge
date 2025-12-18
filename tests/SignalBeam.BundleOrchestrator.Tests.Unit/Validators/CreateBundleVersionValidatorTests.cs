using SignalBeam.BundleOrchestrator.Application.Commands;
using SignalBeam.BundleOrchestrator.Application.Validators;

namespace SignalBeam.BundleOrchestrator.Tests.Unit.Validators;

public class CreateBundleVersionValidatorTests
{
    private readonly CreateBundleVersionValidator _validator;

    public CreateBundleVersionValidatorTests()
    {
        _validator = new CreateBundleVersionValidator();
    }

    [Fact]
    public async Task Validate_ShouldBeValid_WhenAllFieldsAreValid()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto>
            {
                new ContainerSpecDto("web", "nginx:1.21")
            },
            ReleaseNotes: "Initial release");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenBundleIdIsInvalid()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: "invalid-guid",
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "BundleId" && e.ErrorMessage.Contains("GUID"));
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenVersionFormatIsInvalid()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "invalid-version",
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Version" && e.ErrorMessage.Contains("semantic version"));
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("1.0.0-beta.1")]
    [InlineData("2.15.3-alpha")]
    [InlineData("10.0.99")]
    public async Task Validate_ShouldBeValid_WhenVersionFormatIsValid(string version)
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: version,
            Containers: new List<ContainerSpecDto> { new ContainerSpecDto("web", "nginx:1.21") });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenContainersIsEmpty()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto>());

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Containers");
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenContainerNameIsEmpty()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto>
            {
                new ContainerSpecDto("", "nginx:1.21")
            });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Name"));
    }

    [Fact]
    public async Task Validate_ShouldBeInvalid_WhenContainerImageIsEmpty()
    {
        // Arrange
        var command = new CreateBundleVersionCommand(
            BundleId: Guid.NewGuid().ToString(),
            Version: "1.0.0",
            Containers: new List<ContainerSpecDto>
            {
                new ContainerSpecDto("web", "")
            });

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Image"));
    }
}
