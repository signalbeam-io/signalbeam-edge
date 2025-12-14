using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Validation_ShouldCreateValidationError()
    {
        // Act
        var error = Error.Validation("VAL_001", "Validation failed");

        // Assert
        error.Code.Should().Be("VAL_001");
        error.Message.Should().Be("Validation failed");
        error.Type.Should().Be(ErrorType.Validation);
        error.Metadata.Should().BeNull();
    }

    [Fact]
    public void Validation_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["Field"] = "Email",
            ["Reason"] = "Invalid format"
        };

        // Act
        var error = Error.Validation("VAL_002", "Invalid email", metadata);

        // Assert
        error.Code.Should().Be("VAL_002");
        error.Message.Should().Be("Invalid email");
        error.Type.Should().Be(ErrorType.Validation);
        error.Metadata.Should().NotBeNull();
        error.Metadata!["Field"].Should().Be("Email");
        error.Metadata["Reason"].Should().Be("Invalid format");
    }

    [Fact]
    public void NotFound_ShouldCreateNotFoundError()
    {
        // Act
        var error = Error.NotFound("NF_001", "Resource not found");

        // Assert
        error.Code.Should().Be("NF_001");
        error.Message.Should().Be("Resource not found");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Conflict_ShouldCreateConflictError()
    {
        // Act
        var error = Error.Conflict("CONF_001", "Resource already exists");

        // Assert
        error.Code.Should().Be("CONF_001");
        error.Message.Should().Be("Resource already exists");
        error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Unauthorized_ShouldCreateUnauthorizedError()
    {
        // Act
        var error = Error.Unauthorized("AUTH_001", "Invalid credentials");

        // Assert
        error.Code.Should().Be("AUTH_001");
        error.Message.Should().Be("Invalid credentials");
        error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public void Forbidden_ShouldCreateForbiddenError()
    {
        // Act
        var error = Error.Forbidden("FORB_001", "Access denied");

        // Assert
        error.Code.Should().Be("FORB_001");
        error.Message.Should().Be("Access denied");
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Failure_ShouldCreateFailureError()
    {
        // Act
        var error = Error.Failure("FAIL_001", "Operation failed");

        // Assert
        error.Code.Should().Be("FAIL_001");
        error.Message.Should().Be("Operation failed");
        error.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Unexpected_ShouldCreateUnexpectedError()
    {
        // Act
        var error = Error.Unexpected("UNX_001", "Unexpected error occurred");

        // Assert
        error.Code.Should().Be("UNX_001");
        error.Message.Should().Be("Unexpected error occurred");
        error.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void None_ShouldCreateNoneError()
    {
        // Act
        var error = Error.None;

        // Assert
        error.Code.Should().Be(string.Empty);
        error.Message.Should().Be(string.Empty);
        error.Type.Should().Be(ErrorType.None);
    }
}
