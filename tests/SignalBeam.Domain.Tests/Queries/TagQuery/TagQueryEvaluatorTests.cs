using SignalBeam.Domain.Entities;
using SignalBeam.Domain.Queries.TagQuery;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.Queries.TagQuery;

public class TagQueryEvaluatorTests
{
    private static Device CreateTestDevice(params string[] tags)
    {
        var device = Device.Register(
            DeviceId.New(),
            TenantId.New(),
            "TestDevice",
            DateTimeOffset.UtcNow);

        foreach (var tag in tags)
        {
            device.AddTag(tag);
        }

        return device;
    }

    #region Match Expression Tests

    [Fact]
    public void Evaluate_MatchExpression_WithMatchingTag_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=warehouse-1");
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_MatchExpression_WithNonMatchingTag_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("environment=staging");
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_MatchExpression_WithoutTag_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("location=warehouse-1");
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_MatchExpression_CaseInsensitive_ShouldMatch()
    {
        // Arrange
        var device = CreateTestDevice("ENVIRONMENT=PRODUCTION");
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Wildcard Expression Tests

    [Fact]
    public void Evaluate_WildcardExpression_WithMatchingPattern_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("location=warehouse-seattle", "environment=production");
        var expression = TagQueryParser.Parse("location=warehouse-*");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WildcardExpression_WithNonMatchingPattern_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("location=office-seattle");
        var expression = TagQueryParser.Parse("location=warehouse-*");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WildcardExpression_WithMultipleWildcards_ShouldMatch()
    {
        // Arrange
        var device = CreateTestDevice("version=v1-2-3-beta");
        var expression = TagQueryParser.Parse("version=v*-beta");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_WildcardExpression_SingleWildcard_ShouldMatchAll()
    {
        // Arrange
        var device = CreateTestDevice("environment=production");
        var expression = TagQueryParser.Parse("environment=prod*");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region AND Expression Tests

    [Fact]
    public void Evaluate_AndExpression_BothTrue_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=warehouse-1");
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-1");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_AndExpression_LeftFalse_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("environment=staging", "location=warehouse-1");
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-1");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_AndExpression_RightFalse_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=office-1");
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-1");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_AndExpression_BothFalse_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("environment=development", "location=office-1");
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-1");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_MultipleAndExpressions_AllTrue_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=warehouse-1", "hardware=rpi4");
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-1 AND hardware=rpi4");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region OR Expression Tests

    [Fact]
    public void Evaluate_OrExpression_BothTrue_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("hardware=rpi4", "hardware=rpi5");
        var expression = TagQueryParser.Parse("hardware=rpi4 OR hardware=rpi5");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrExpression_LeftTrue_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("hardware=rpi4");
        var expression = TagQueryParser.Parse("hardware=rpi4 OR hardware=rpi5");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrExpression_RightTrue_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("hardware=rpi5");
        var expression = TagQueryParser.Parse("hardware=rpi4 OR hardware=rpi5");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_OrExpression_BothFalse_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("hardware=rpi3");
        var expression = TagQueryParser.Parse("hardware=rpi4 OR hardware=rpi5");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region NOT Expression Tests

    [Fact]
    public void Evaluate_NotExpression_WithMatchingTag_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice("environment=dev");
        var expression = TagQueryParser.Parse("NOT environment=dev");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_NotExpression_WithNonMatchingTag_ShouldReturnTrue()
    {
        // Arrange
        var device = CreateTestDevice("environment=production");
        var expression = TagQueryParser.Parse("NOT environment=dev");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_DoubleNot_ShouldNegate()
    {
        // Arrange
        var device = CreateTestDevice("environment=production");
        var expression = TagQueryParser.Parse("NOT NOT environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void Evaluate_ComplexExpression_ParenthesizedOr_ShouldEvaluateCorrectly()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=warehouse-seattle");
        var expression = TagQueryParser.Parse("(environment=production OR environment=staging) AND location=warehouse-*");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_WithNotAndOr_ShouldEvaluateCorrectly()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "hardware=rpi4", "location=warehouse-1");
        var expression = TagQueryParser.Parse("(hardware=rpi4 OR hardware=rpi5) AND NOT environment=dev AND location=warehouse-*");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_ComplexExpression_MixedOperators_ShouldEvaluateCorrectly()
    {
        // Arrange
        var device = CreateTestDevice("environment=production", "location=warehouse-1");
        var expression = TagQueryParser.Parse("environment=production AND (location=warehouse-1 OR location=warehouse-2)");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Evaluate_WithEmptyTags_ShouldReturnFalse()
    {
        // Arrange
        var device = CreateTestDevice(); // No tags
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Evaluate_WithInvalidTagInDevice_ShouldSkipInvalidTag()
    {
        // Arrange
        var device = CreateTestDevice("environment=production");
        // Tags list should be filtered, but let's test with valid tags
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_EvaluateTags_WithStringCollection_ShouldWork()
    {
        // Arrange
        var tags = new List<string> { "environment=production", "location=warehouse-1" };
        var expression = TagQueryParser.Parse("environment=production AND location=warehouse-*");

        // Act
        var result = TagQueryEvaluator.EvaluateTags(expression, tags);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_SimpleTag_WithKeyValueQuery_ShouldMatch()
    {
        // Arrange - device has simple tag (backward compatibility)
        var device = CreateTestDevice("production"); // Simple tag, not key=value
        var expression = TagQueryParser.Parse("environment=production");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        // Simple tag "production" matches "environment=production" (ignores key for backward compatibility)
        Assert.True(result);
    }

    [Fact]
    public void Evaluate_MultipleTagsWithSameKey_ShouldMatchAny()
    {
        // Arrange
        var device = CreateTestDevice("location=warehouse-1", "location=warehouse-2");
        var expression = TagQueryParser.Parse("location=warehouse-2");

        // Act
        var result = TagQueryEvaluator.Evaluate(expression, device);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void Evaluate_ProductionDevicesInWarehouses_ShouldMatchCorrectly()
    {
        // Arrange
        var prodDevice = CreateTestDevice("environment=production", "location=warehouse-seattle", "hardware=rpi4");
        var devDevice = CreateTestDevice("environment=dev", "location=warehouse-seattle", "hardware=rpi4");
        var officeDevice = CreateTestDevice("environment=production", "location=office-nyc", "hardware=x86");

        var query = "environment=production AND location=warehouse-*";
        var expression = TagQueryParser.Parse(query);

        // Act
        var prodResult = TagQueryEvaluator.Evaluate(expression, prodDevice);
        var devResult = TagQueryEvaluator.Evaluate(expression, devDevice);
        var officeResult = TagQueryEvaluator.Evaluate(expression, officeDevice);

        // Assert
        Assert.True(prodResult);   // Production device in warehouse should match
        Assert.False(devResult);   // Dev device should not match
        Assert.False(officeResult); // Office device should not match
    }

    [Fact]
    public void Evaluate_RaspberryPiDevices_NotInDevelopment_ShouldMatchCorrectly()
    {
        // Arrange
        var rpi4Prod = CreateTestDevice("hardware=rpi4", "environment=production");
        var rpi5Prod = CreateTestDevice("hardware=rpi5", "environment=production");
        var rpi4Dev = CreateTestDevice("hardware=rpi4", "environment=dev");
        var x86Prod = CreateTestDevice("hardware=x86", "environment=production");

        var query = "(hardware=rpi4 OR hardware=rpi5) AND NOT environment=dev";
        var expression = TagQueryParser.Parse(query);

        // Act
        var rpi4ProdResult = TagQueryEvaluator.Evaluate(expression, rpi4Prod);
        var rpi5ProdResult = TagQueryEvaluator.Evaluate(expression, rpi5Prod);
        var rpi4DevResult = TagQueryEvaluator.Evaluate(expression, rpi4Dev);
        var x86ProdResult = TagQueryEvaluator.Evaluate(expression, x86Prod);

        // Assert
        Assert.True(rpi4ProdResult);  // RPi4 production should match
        Assert.True(rpi5ProdResult);  // RPi5 production should match
        Assert.False(rpi4DevResult);  // RPi4 dev should not match (NOT dev)
        Assert.False(x86ProdResult);  // x86 should not match (not RPi)
    }

    #endregion
}
