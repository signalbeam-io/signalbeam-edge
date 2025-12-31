using System.Text;

namespace SignalBeam.Domain.Queries.TagQuery;

/// <summary>
/// Recursive descent parser for tag query expressions.
/// Parses query strings into expression trees (AST).
/// Thread-safe and stateless.
/// </summary>
public sealed class TagQueryParser
{
    /// <summary>
    /// Parses a tag query string into an expression tree.
    /// </summary>
    /// <param name="query">Query string (e.g., "environment=production AND location=warehouse-*")</param>
    /// <returns>Parsed expression tree</returns>
    /// <exception cref="ArgumentException">Thrown if query is invalid</exception>
    /// <exception cref="FormatException">Thrown if query syntax is invalid</exception>
    public static TagQueryExpression Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query cannot be empty.", nameof(query));
        }

        var tokenizer = new Tokenizer(query);
        var parser = new Parser(tokenizer);

        var expression = parser.ParseQuery();

        if (!parser.IsAtEnd())
        {
            throw new FormatException($"Unexpected token at position {parser.Position}: '{parser.Current}'");
        }

        return expression;
    }

    #region Tokenizer

    private enum TokenType
    {
        And,        // AND
        Or,         // OR
        Not,        // NOT
        LeftParen,  // (
        RightParen, // )
        Equals,     // =
        Identifier, // key or value
        Eof
    }

    private sealed record Token(TokenType Type, string Value, int Position);

    private sealed class Tokenizer
    {
        private readonly string _input;
        private int _position;

        public Tokenizer(string input)
        {
            _input = input;
            _position = 0;
        }

        public Token NextToken()
        {
            SkipWhitespace();

            if (IsAtEnd())
            {
                return new Token(TokenType.Eof, string.Empty, _position);
            }

            var start = _position;

            // Single character tokens
            switch (Current())
            {
                case '(':
                    Advance();
                    return new Token(TokenType.LeftParen, "(", start);
                case ')':
                    Advance();
                    return new Token(TokenType.RightParen, ")", start);
                case '=':
                    Advance();
                    return new Token(TokenType.Equals, "=", start);
            }

            // Keywords and identifiers
            if (char.IsLetter(Current()))
            {
                return ReadIdentifierOrKeyword(start);
            }

            throw new FormatException($"Unexpected character '{Current()}' at position {_position}");
        }

        private Token ReadIdentifierOrKeyword(int start)
        {
            var buffer = new StringBuilder();

            while (!IsAtEnd() && (char.IsLetterOrDigit(Current()) || Current() == '_' || Current() == '-' || Current() == '*'))
            {
                buffer.Append(Current());
                Advance();
            }

            var value = buffer.ToString();

            // Check for keywords (case-insensitive)
            return value.ToUpperInvariant() switch
            {
                "AND" => new Token(TokenType.And, value, start),
                "OR" => new Token(TokenType.Or, value, start),
                "NOT" => new Token(TokenType.Not, value, start),
                _ => new Token(TokenType.Identifier, value, start)
            };
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd() && char.IsWhiteSpace(Current()))
            {
                Advance();
            }
        }

        private char Current() => _input[_position];
        private void Advance() => _position++;
        private bool IsAtEnd() => _position >= _input.Length;
    }

    #endregion

    #region Parser

    private sealed class Parser
    {
        private readonly Tokenizer _tokenizer;
        private Token _current;

        public Parser(Tokenizer tokenizer)
        {
            _tokenizer = tokenizer;
            _current = _tokenizer.NextToken();
        }

        public int Position => _current.Position;
        public string Current => _current.Value;

        public TagQueryExpression ParseQuery()
        {
            return ParseOrExpression();
        }

        private TagQueryExpression ParseOrExpression()
        {
            var left = ParseAndExpression();

            while (Match(TokenType.Or))
            {
                Consume(TokenType.Or);
                var right = ParseAndExpression();
                left = new OrExpression(left, right);
            }

            return left;
        }

        private TagQueryExpression ParseAndExpression()
        {
            var left = ParseNotExpression();

            while (Match(TokenType.And))
            {
                Consume(TokenType.And);
                var right = ParseNotExpression();
                left = new AndExpression(left, right);
            }

            return left;
        }

        private TagQueryExpression ParseNotExpression()
        {
            if (Match(TokenType.Not))
            {
                Consume(TokenType.Not);
                var operand = ParseNotExpression();
                return new NotExpression(operand);
            }

            return ParsePrimary();
        }

        private TagQueryExpression ParsePrimary()
        {
            // Parenthesized expression
            if (Match(TokenType.LeftParen))
            {
                Consume(TokenType.LeftParen);
                var expr = ParseOrExpression();
                Consume(TokenType.RightParen, "Expected ')' after expression");
                return expr;
            }

            // Match expression: key=value
            return ParseMatch();
        }

        private TagQueryExpression ParseMatch()
        {
            var key = Consume(TokenType.Identifier, "Expected tag key").Value;
            Consume(TokenType.Equals, "Expected '=' after tag key");
            var value = Consume(TokenType.Identifier, "Expected tag value").Value;

            // Normalize to lowercase
            key = key.ToLowerInvariant();
            value = value.ToLowerInvariant();

            // Check if value contains wildcard
            if (value.Contains('*'))
            {
                return new WildcardExpression(key, value);
            }

            return new MatchExpression(key, value);
        }

        private bool Match(TokenType type) => _current.Type == type;

        private Token Consume(TokenType type, string? errorMessage = null)
        {
            if (!Match(type))
            {
                throw new FormatException(
                    errorMessage ?? $"Expected {type} but got {_current.Type} at position {_current.Position}");
            }

            var token = _current;
            _current = _tokenizer.NextToken();
            return token;
        }

        public bool IsAtEnd() => _current.Type == TokenType.Eof;
    }

    #endregion
}
