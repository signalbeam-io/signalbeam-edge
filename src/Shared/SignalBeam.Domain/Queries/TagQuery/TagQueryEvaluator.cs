using SignalBeam.Domain.Entities;
using SignalBeam.Domain.ValueObjects;

namespace SignalBeam.Domain.Queries.TagQuery;

/// <summary>
/// Evaluates tag query expressions against device tags.
/// Implements visitor pattern for expression tree traversal.
/// </summary>
public sealed class TagQueryEvaluator : ITagQueryExpressionVisitor<bool>
{
    private readonly IReadOnlyCollection<DeviceTag> _deviceTags;

    private TagQueryEvaluator(IReadOnlyCollection<DeviceTag> deviceTags)
    {
        _deviceTags = deviceTags;
    }

    /// <summary>
    /// Evaluates a query expression against a device's tags.
    /// </summary>
    /// <param name="expression">Parsed query expression</param>
    /// <param name="device">Device to evaluate</param>
    /// <returns>True if device matches the query</returns>
    public static bool Evaluate(TagQueryExpression expression, Device device)
    {
        // Convert string tags to DeviceTag value objects, skip invalid tags
        var deviceTags = device.Tags
            .Select(tagString =>
            {
                try
                {
                    return DeviceTag.Create(tagString);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            })
            .Where(tag => tag is not null)
            .Select(tag => tag!)
            .ToList();

        var evaluator = new TagQueryEvaluator(deviceTags);
        return expression.Accept(evaluator);
    }

    /// <summary>
    /// Evaluates a query expression against a collection of tag strings.
    /// </summary>
    /// <param name="expression">Parsed query expression</param>
    /// <param name="tags">Collection of tag strings</param>
    /// <returns>True if tags match the query</returns>
    public static bool EvaluateTags(TagQueryExpression expression, IReadOnlyCollection<string> tags)
    {
        // Convert string tags to DeviceTag value objects, skip invalid tags
        var deviceTags = tags
            .Select(tagString =>
            {
                try
                {
                    return DeviceTag.Create(tagString);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            })
            .Where(tag => tag is not null)
            .Select(tag => tag!)
            .ToList();

        var evaluator = new TagQueryEvaluator(deviceTags);
        return expression.Accept(evaluator);
    }

    public bool Visit(AndExpression expression)
    {
        return expression.Left.Accept(this) && expression.Right.Accept(this);
    }

    public bool Visit(OrExpression expression)
    {
        return expression.Left.Accept(this) || expression.Right.Accept(this);
    }

    public bool Visit(NotExpression expression)
    {
        return !expression.Operand.Accept(this);
    }

    public bool Visit(MatchExpression expression)
    {
        return _deviceTags.Any(tag => tag.Matches(expression.Key, expression.Value));
    }

    public bool Visit(WildcardExpression expression)
    {
        return _deviceTags.Any(tag => tag.Matches(expression.Key, expression.Pattern));
    }
}
