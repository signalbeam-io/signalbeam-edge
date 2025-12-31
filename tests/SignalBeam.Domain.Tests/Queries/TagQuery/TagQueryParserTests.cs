using SignalBeam.Domain.Queries.TagQuery;

namespace SignalBeam.Domain.Tests.Queries.TagQuery;

public class TagQueryParserTests
{
    #region Valid Queries

    [Fact]
    public void Parse_SimpleMatch_ShouldReturnMatchExpression()
    {
        // Arrange
        var query = "environment=production";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<MatchExpression>(result);
        var match = (MatchExpression)result;
        Assert.Equal("environment", match.Key);
        Assert.Equal("production", match.Value);
    }

    [Fact]
    public void Parse_WildcardMatch_ShouldReturnWildcardExpression()
    {
        // Arrange
        var query = "location=warehouse-*";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<WildcardExpression>(result);
        var wildcard = (WildcardExpression)result;
        Assert.Equal("location", wildcard.Key);
        Assert.Equal("warehouse-*", wildcard.Pattern);
    }

    [Fact]
    public void Parse_AndExpression_ShouldReturnAndExpression()
    {
        // Arrange
        var query = "environment=production AND location=warehouse-1";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AndExpression>(result);
        var and = (AndExpression)result;

        Assert.IsType<MatchExpression>(and.Left);
        Assert.IsType<MatchExpression>(and.Right);

        var left = (MatchExpression)and.Left;
        var right = (MatchExpression)and.Right;

        Assert.Equal("environment", left.Key);
        Assert.Equal("production", left.Value);
        Assert.Equal("location", right.Key);
        Assert.Equal("warehouse-1", right.Value);
    }

    [Fact]
    public void Parse_OrExpression_ShouldReturnOrExpression()
    {
        // Arrange
        var query = "hardware=rpi4 OR hardware=rpi5";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrExpression>(result);
        var or = (OrExpression)result;

        Assert.IsType<MatchExpression>(or.Left);
        Assert.IsType<MatchExpression>(or.Right);
    }

    [Fact]
    public void Parse_NotExpression_ShouldReturnNotExpression()
    {
        // Arrange
        var query = "NOT environment=dev";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<NotExpression>(result);
        var not = (NotExpression)result;

        Assert.IsType<MatchExpression>(not.Operand);
        var match = (MatchExpression)not.Operand;
        Assert.Equal("environment", match.Key);
        Assert.Equal("dev", match.Value);
    }

    [Fact]
    public void Parse_ParenthesizedExpression_ShouldRespectPrecedence()
    {
        // Arrange
        var query = "(environment=production OR environment=staging) AND location=warehouse-*";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AndExpression>(result);
        var and = (AndExpression)result;

        // Left should be the parenthesized OR
        Assert.IsType<OrExpression>(and.Left);
        // Right should be wildcard match
        Assert.IsType<WildcardExpression>(and.Right);
    }

    [Fact]
    public void Parse_ComplexNestedExpression_ShouldParseCorrectly()
    {
        // Arrange
        var query = "(hardware=rpi4 OR hardware=rpi5) AND NOT environment=dev AND location=warehouse-*";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        // Should be a chain of AND expressions
        Assert.IsType<AndExpression>(result);
    }

    [Fact]
    public void Parse_CaseInsensitiveKeywords_ShouldWorkWithAnyCase()
    {
        // Arrange - lowercase keywords
        var query1 = "environment=production and location=warehouse-1";
        var query2 = "environment=production or location=warehouse-1";
        var query3 = "not environment=dev";

        // Act
        var result1 = TagQueryParser.Parse(query1);
        var result2 = TagQueryParser.Parse(query2);
        var result3 = TagQueryParser.Parse(query3);

        // Assert
        Assert.IsType<AndExpression>(result1);
        Assert.IsType<OrExpression>(result2);
        Assert.IsType<NotExpression>(result3);
    }

    [Fact]
    public void Parse_WithWhitespace_ShouldIgnoreExtraWhitespace()
    {
        // Arrange
        var query = "  environment  =  production   AND   location  =  warehouse-1  ";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AndExpression>(result);
    }

    [Fact]
    public void Parse_MultipleNot_ShouldParseCorrectly()
    {
        // Arrange
        var query = "NOT NOT environment=production";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<NotExpression>(result);
        var not1 = (NotExpression)result;
        Assert.IsType<NotExpression>(not1.Operand);
    }

    [Fact]
    public void Parse_TagsWithHyphensAndUnderscores_ShouldParse()
    {
        // Arrange
        var query = "device-type=edge_device AND firmware-version=v1_2_3";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AndExpression>(result);
    }

    #endregion

    #region Invalid Queries

    [Fact]
    public void Parse_EmptyQuery_ShouldThrowArgumentException()
    {
        // Arrange
        var query = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_WhitespaceOnly_ShouldThrowArgumentException()
    {
        // Arrange
        var query = "   ";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_MissingValue_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment=";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_MissingKey_ShouldThrowFormatException()
    {
        // Arrange
        var query = "=production";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_MissingEquals_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment production";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_UnmatchedLeftParen_ShouldThrowFormatException()
    {
        // Arrange
        var query = "(environment=production";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_UnmatchedRightParen_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment=production)";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_InvalidCharacter_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment=production @ location=warehouse";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_AndWithoutRightOperand_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment=production AND";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_OrWithoutRightOperand_ShouldThrowFormatException()
    {
        // Arrange
        var query = "environment=production OR";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_NotWithoutOperand_ShouldThrowFormatException()
    {
        // Arrange
        var query = "NOT";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    [Fact]
    public void Parse_EmptyParentheses_ShouldThrowFormatException()
    {
        // Arrange
        var query = "()";

        // Act & Assert
        Assert.Throws<FormatException>(() => TagQueryParser.Parse(query));
    }

    #endregion

    #region Operator Precedence

    [Fact]
    public void Parse_AndOrPrecedence_ShouldBindAndTighter()
    {
        // Arrange
        // AND has higher precedence than OR, so this parses as: a=one OR (b=two AND c=three)
        var query = "a=one OR b=two AND c=three";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<OrExpression>(result);
        var or = (OrExpression)result;

        // Left should be simple match
        Assert.IsType<MatchExpression>(or.Left);

        // Right should be AND expression
        Assert.IsType<AndExpression>(or.Right);
        var and = (AndExpression)or.Right;
        Assert.IsType<MatchExpression>(and.Left);
        Assert.IsType<MatchExpression>(and.Right);
    }

    [Fact]
    public void Parse_NotAndPrecedence_ShouldBindNotTighter()
    {
        // Arrange
        // Should parse as: (NOT a=one) AND b=two
        var query = "NOT a=one AND b=two";

        // Act
        var result = TagQueryParser.Parse(query);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AndExpression>(result);
        var and = (AndExpression)result;

        // Left should be NOT expression
        Assert.IsType<NotExpression>(and.Left);
        // Right should be match
        Assert.IsType<MatchExpression>(and.Right);
    }

    #endregion
}
