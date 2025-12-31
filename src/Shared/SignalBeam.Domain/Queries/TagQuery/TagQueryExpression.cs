namespace SignalBeam.Domain.Queries.TagQuery;

/// <summary>
/// Abstract base class for tag query expressions.
/// Represents a node in the expression tree (AST).
/// </summary>
public abstract record TagQueryExpression
{
    /// <summary>
    /// Accepts a visitor for traversal and evaluation.
    /// Uses Visitor pattern for extensibility.
    /// </summary>
    public abstract T Accept<T>(ITagQueryExpressionVisitor<T> visitor);
}

/// <summary>
/// Visitor interface for tag query expressions.
/// Enables different evaluation strategies (in-memory, SQL, etc.)
/// </summary>
public interface ITagQueryExpressionVisitor<out T>
{
    T Visit(AndExpression expression);
    T Visit(OrExpression expression);
    T Visit(NotExpression expression);
    T Visit(MatchExpression expression);
    T Visit(WildcardExpression expression);
}

/// <summary>
/// Represents logical AND operation.
/// Example: environment=production AND location=warehouse-1
/// </summary>
public sealed record AndExpression(
    TagQueryExpression Left,
    TagQueryExpression Right) : TagQueryExpression
{
    public override T Accept<T>(ITagQueryExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents logical OR operation.
/// Example: hardware=rpi4 OR hardware=rpi5
/// </summary>
public sealed record OrExpression(
    TagQueryExpression Left,
    TagQueryExpression Right) : TagQueryExpression
{
    public override T Accept<T>(ITagQueryExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents logical NOT operation.
/// Example: NOT environment=dev
/// </summary>
public sealed record NotExpression(
    TagQueryExpression Operand) : TagQueryExpression
{
    public override T Accept<T>(ITagQueryExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents exact match expression.
/// Example: environment=production
/// </summary>
public sealed record MatchExpression(
    string Key,
    string Value) : TagQueryExpression
{
    public override T Accept<T>(ITagQueryExpressionVisitor<T> visitor) => visitor.Visit(this);
}

/// <summary>
/// Represents wildcard match expression.
/// Example: location=warehouse-*
/// </summary>
public sealed record WildcardExpression(
    string Key,
    string Pattern) : TagQueryExpression
{
    public override T Accept<T>(ITagQueryExpressionVisitor<T> visitor) => visitor.Visit(this);
}
