using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Tests.ValueObjects;

public class DeviceTagTests
{
    #region Creation Tests

    [Fact]
    public void Create_WithKeyValueFormat_ShouldParseCorrectly()
    {
        // Arrange
        var tagString = "environment=production";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("environment", tag.Key);
        Assert.Equal("production", tag.Value);
        Assert.True(tag.IsKeyValue);
    }

    [Fact]
    public void Create_WithSimpleFormat_ShouldSetKeyAndValueEqual()
    {
        // Arrange
        var tagString = "production";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("production", tag.Key);
        Assert.Equal("production", tag.Value);
        Assert.False(tag.IsKeyValue);
    }

    [Fact]
    public void Create_WithUpperCase_ShouldNormalizeToLowerCase()
    {
        // Arrange
        var tagString = "ENVIRONMENT=PRODUCTION";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("environment", tag.Key);
        Assert.Equal("production", tag.Value);
    }

    [Fact]
    public void Create_WithMixedCase_ShouldNormalizeToLowerCase()
    {
        // Arrange
        var tagString = "Environment=Production";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("environment", tag.Key);
        Assert.Equal("production", tag.Value);
    }

    [Fact]
    public void Create_WithWhitespace_ShouldTrimAndNormalize()
    {
        // Arrange
        var tagString = "  environment = production  ";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("environment", tag.Key);
        Assert.Equal("production", tag.Value);
    }

    [Fact]
    public void Create_WithHyphensAndUnderscores_ShouldAccept()
    {
        // Arrange
        var tagString = "device-type=edge_device";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("device-type", tag.Key);
        Assert.Equal("edge_device", tag.Value);
    }

    [Fact]
    public void Create_WithWildcardInValue_ShouldAccept()
    {
        // Arrange
        var tagString = "location=warehouse-*";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("location", tag.Key);
        Assert.Equal("warehouse-*", tag.Value);
    }

    [Fact]
    public void Create_WithNumericValue_ShouldAccept()
    {
        // Arrange
        var tagString = "version=123";

        // Act
        var tag = DeviceTag.Create(tagString);

        // Assert
        Assert.Equal("version", tag.Key);
        Assert.Equal("123", tag.Value);
    }

    #endregion

    #region Invalid Input Tests

    [Fact]
    public void Create_WithEmptyString_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithWhitespaceOnly_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "   ";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithEmptyKey_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "=production";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithEmptyValue_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "environment=";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithInvalidCharactersInKey_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "environment!@#=production";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithInvalidCharactersInValue_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "environment=production!@#";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithSpacesInKey_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "device type=edge";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Create_WithWildcardInKey_ShouldThrowArgumentException()
    {
        // Arrange
        var tagString = "environment*=production";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    #endregion

    #region Matching Tests

    [Fact]
    public void Matches_ExactMatch_ShouldReturnTrue()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act
        var result = tag.Matches("environment", "production");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_DifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act
        var result = tag.Matches("environment", "staging");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Matches_DifferentKey_ShouldReturnFalse()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act
        var result = tag.Matches("location", "production");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Matches_CaseInsensitive_ShouldReturnTrue()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act
        var result = tag.Matches("ENVIRONMENT", "PRODUCTION");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WithWildcardPrefix_ShouldMatch()
    {
        // Arrange
        var tag = DeviceTag.Create("location=warehouse-seattle");

        // Act
        var result = tag.Matches("location", "warehouse-*");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WithWildcardSuffix_ShouldMatch()
    {
        // Arrange
        var tag = DeviceTag.Create("location=seattle-warehouse");

        // Act
        var result = tag.Matches("location", "*-warehouse");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WithWildcardMiddle_ShouldMatch()
    {
        // Arrange
        var tag = DeviceTag.Create("version=v1-2-3-beta");

        // Act
        var result = tag.Matches("version", "v*-beta");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WithSingleWildcard_ShouldMatchAll()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act - wildcard matches any value
        var result = tag.Matches("environment", "prod*");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WildcardNoMatch_ShouldReturnFalse()
    {
        // Arrange
        var tag = DeviceTag.Create("location=office-seattle");

        // Act
        var result = tag.Matches("location", "warehouse-*");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Matches_SimpleTag_ShouldMatchValueRegardlessOfKey()
    {
        // Arrange - simple tag (backward compatibility)
        var tag = DeviceTag.Create("production");

        // Act
        var result = tag.Matches("environment", "production");

        // Assert
        // Simple tags ignore the key and match only on value
        Assert.True(result);
    }

    [Fact]
    public void Matches_SimpleTag_WithSameValue_ShouldMatch()
    {
        // Arrange
        var tag = DeviceTag.Create("production");

        // Act
        // For simple tags, the key is ignored in matching logic if the value matches
        var result = tag.Matches("production", "production");

        // Assert
        Assert.True(result);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithKeyValue_ShouldReturnKeyValueFormat()
    {
        // Arrange
        var tag = DeviceTag.Create("environment=production");

        // Act
        var result = tag.ToString();

        // Assert
        Assert.Equal("environment=production", result);
    }

    [Fact]
    public void ToString_WithSimpleTag_ShouldReturnValue()
    {
        // Arrange
        var tag = DeviceTag.Create("production");

        // Act
        var result = tag.ToString();

        // Assert
        Assert.Equal("production", result);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameTags_ShouldBeEqual()
    {
        // Arrange
        var tag1 = DeviceTag.Create("environment=production");
        var tag2 = DeviceTag.Create("environment=production");

        // Act & Assert
        Assert.Equal(tag1, tag2);
        Assert.True(tag1 == tag2);
        Assert.False(tag1 != tag2);
    }

    [Fact]
    public void Equality_DifferentKeys_ShouldNotBeEqual()
    {
        // Arrange
        var tag1 = DeviceTag.Create("environment=production");
        var tag2 = DeviceTag.Create("location=production");

        // Act & Assert
        Assert.NotEqual(tag1, tag2);
        Assert.False(tag1 == tag2);
        Assert.True(tag1 != tag2);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var tag1 = DeviceTag.Create("environment=production");
        var tag2 = DeviceTag.Create("environment=staging");

        // Act & Assert
        Assert.NotEqual(tag1, tag2);
    }

    [Fact]
    public void Equality_CaseNormalized_ShouldBeEqual()
    {
        // Arrange
        var tag1 = DeviceTag.Create("ENVIRONMENT=PRODUCTION");
        var tag2 = DeviceTag.Create("environment=production");

        // Act & Assert
        Assert.Equal(tag1, tag2);
    }

    [Fact]
    public void Equality_SimpleVsKeyValue_ShouldNotBeEqual()
    {
        // Arrange
        var simpleTag = DeviceTag.Create("production");
        var keyValueTag = DeviceTag.Create("environment=production");

        // Act & Assert
        Assert.NotEqual(simpleTag, keyValueTag);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Create_WithMultipleEquals_ShouldThrowArgumentException()
    {
        // Arrange - value contains '=' which is not allowed
        var tagString = "formula=a=b";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => DeviceTag.Create(tagString));
    }

    [Fact]
    public void Matches_WithSpecialRegexCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var tag = DeviceTag.Create("pattern=test-1-2-3");

        // Act - pattern contains hyphens which are regex-safe but testing escape logic
        var result = tag.Matches("pattern", "test-1-2-3");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Matches_WildcardWithComplexPattern_ShouldMatch()
    {
        // Arrange
        var tag = DeviceTag.Create("firmware=v1-2-3-alpha-build-456");

        // Act
        var result = tag.Matches("firmware", "v1-*-alpha-*");

        // Assert
        Assert.True(result);
    }

    #endregion
}
