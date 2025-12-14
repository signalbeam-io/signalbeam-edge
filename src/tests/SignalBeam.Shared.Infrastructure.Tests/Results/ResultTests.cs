using FluentAssertions;
using SignalBeam.Shared.Infrastructure.Results;

namespace SignalBeam.Shared.Infrastructure.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessResult()
    {
        // Act
        var result = Result.Success();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var error = Error.Failure("TEST_ERROR", "Test error message");

        // Act
        var result = Result.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SuccessGeneric_ShouldCreateSuccessResultWithValue()
    {
        // Arrange
        var value = "test value";

        // Act
        var result = Result.Success(value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(value);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailureGeneric_ShouldCreateFailureResultWithoutValue()
    {
        // Arrange
        var error = Error.NotFound("NOT_FOUND", "Resource not found");

        // Act
        var result = Result.Failure<string>(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Value_OnFailureResult_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var error = Error.Failure("TEST_ERROR", "Test error");
        var result = Result.Failure<string>(error);

        // Act
        var act = () => result.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access value of a failed result.");
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertValueToSuccessResult()
    {
        // Arrange
        var value = 42;

        // Act
        Result<int> result = value;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(value);
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertErrorToFailureResult()
    {
        // Arrange
        var error = Error.Validation("VALIDATION_ERROR", "Invalid input");

        // Act
        Result<int> result = error;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }
}
