using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Tests.Results;

public class ErrorTests
{
    [Fact]
    public void Validation_ShouldCreateValidationError()
    {
        // Act
        var error = Error.Validation("VAL001", "Invalid input");

        // Assert
        error.Code.Should().Be("VAL001");
        error.Message.Should().Be("Invalid input");
        error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void NotFound_ShouldCreateNotFoundError()
    {
        // Act
        var error = Error.NotFound("NOT_FOUND", "Resource not found");

        // Assert
        error.Code.Should().Be("NOT_FOUND");
        error.Message.Should().Be("Resource not found");
        error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Conflict_ShouldCreateConflictError()
    {
        // Act
        var error = Error.Conflict("CONFLICT", "Resource already exists");

        // Assert
        error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Unauthorized_ShouldCreateUnauthorizedError()
    {
        // Act
        var error = Error.Unauthorized("UNAUTH", "Invalid credentials");

        // Assert
        error.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public void Forbidden_ShouldCreateForbiddenError()
    {
        // Act
        var error = Error.Forbidden("FORBIDDEN", "Access denied");

        // Assert
        error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Failure_ShouldCreateFailureError()
    {
        // Act
        var error = Error.Failure("FAIL", "Operation failed");

        // Assert
        error.Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Unexpected_ShouldCreateUnexpectedError()
    {
        // Act
        var error = Error.Unexpected("UNEXPECTED", "Unexpected error occurred");

        // Assert
        error.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public void WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["field"] = "username",
            ["attemptedValue"] = "invalid@"
        };

        // Act
        var error = Error.Validation("VAL002", "Invalid format", metadata);

        // Assert
        error.Metadata.Should().NotBeNull();
        error.Metadata.Should().ContainKey("field");
        error.Metadata!["field"].Should().Be("username");
    }

    [Fact]
    public void None_ShouldHaveEmptyCodeAndMessage()
    {
        // Act
        var error = Error.None;

        // Assert
        error.Code.Should().BeEmpty();
        error.Message.Should().BeEmpty();
        error.Type.Should().Be(ErrorType.None);
    }

    [Theory]
    [InlineData(ErrorType.None, 0)]
    [InlineData(ErrorType.Validation, 1)]
    [InlineData(ErrorType.NotFound, 2)]
    [InlineData(ErrorType.Conflict, 3)]
    [InlineData(ErrorType.Unauthorized, 4)]
    [InlineData(ErrorType.Forbidden, 5)]
    [InlineData(ErrorType.Failure, 6)]
    [InlineData(ErrorType.Unexpected, 7)]
    public void ErrorType_ShouldHaveCorrectNumericValue(ErrorType errorType, int expectedValue)
    {
        // Assert
        ((int)errorType).Should().Be(expectedValue);
    }
}
