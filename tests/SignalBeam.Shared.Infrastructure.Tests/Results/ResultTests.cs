using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.Validation("VAL001", "Invalid input");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Success_WithValue_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_WithValue_ShouldCreateFailedResult()
    {
        // Arrange
        var error = Error.NotFound("NOT_FOUND", "Resource not found");

        // Act
        var result = Result.Failure<int>(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_WhenFailure_ShouldThrowException()
    {
        // Arrange
        var error = Error.Failure("ERR001", "Something went wrong");
        var result = Result.Failure<int>(error);

        // Act & Assert
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result.");
    }

    [Fact]
    public void ImplicitConversion_ToBool_ShouldReturnIsSuccess()
    {
        // Arrange
        var successResult = Result.Success();
        var failureResult = Result.Failure(Error.Failure("ERR", "Error"));

        // Act & Assert
        ((bool)successResult).Should().BeTrue();
        ((bool)failureResult).Should().BeFalse();
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccessResult()
    {
        // Act
        Result<string> result = "Hello, World!";

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("Hello, World!");
    }

    [Fact]
    public void ImplicitConversion_FromError_ShouldCreateFailureResult()
    {
        // Arrange
        var error = Error.Unauthorized("UNAUTH", "Unauthorized access");

        // Act
        Result<string> result = error;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_WithSuccessAndError_ShouldThrow()
    {
        // Arrange
        var error = Error.Failure("ERR", "Error");

        // Act & Assert
        var act = () => new TestableResult(true, error);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A successful result cannot have an error.");
    }

    [Fact]
    public void Constructor_WithFailureAndNoError_ShouldThrow()
    {
        // Act & Assert
        var act = () => new TestableResult(false, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A failed result must have an error.");
    }

    // Helper class to test protected constructor
    private class TestableResult : Result
    {
        public TestableResult(bool isSuccess, Error? error) : base(isSuccess, error)
        {
        }
    }
}
