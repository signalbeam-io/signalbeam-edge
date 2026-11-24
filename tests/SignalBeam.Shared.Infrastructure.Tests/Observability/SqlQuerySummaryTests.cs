using FluentAssertions;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.Shared.Infrastructure.Tests.Observability;

public class SqlQuerySummaryTests
{
    [Theory]
    // Shapes taken from real EF Core and raw ADO.NET traces of the SignalBeam services.
    [InlineData("SELECT count(*)::int\nFROM device_manager.devices AS d", "SELECT", "device_manager.devices")]
    [InlineData("SELECT d.id, d.name\nFROM device_manager.devices AS d\nWHERE d.tenant_id = @__tenantId_0", "SELECT", "device_manager.devices")]
    [InlineData("INSERT INTO bundle_orchestrator.\"Rollouts\" (id, name) VALUES (@p0, @p1)", "INSERT", "bundle_orchestrator.Rollouts")]
    [InlineData("UPDATE device_manager.devices SET last_seen_at = @p0 WHERE id = @p1", "UPDATE", "device_manager.devices")]
    [InlineData("DELETE FROM device_manager.devices AS d WHERE d.status = 'Pending'", "DELETE", "device_manager.devices")]
    [InlineData("select * from \"telemetry_processor\".\"device_heartbeats\"", "SELECT", "telemetry_processor.device_heartbeats")]
    [InlineData("  -- comment\n/* block */ SELECT 1", "SELECT", null)]
    [InlineData("TRUNCATE TABLE ONLY telemetry_processor.device_metrics", "TRUNCATE", "telemetry_processor.device_metrics")]
    public void Parse_CommonStatements_ReturnsOperationAndCollection(string sql, string operation, string? collection)
    {
        // Act
        var result = SqlQuerySummary.Parse(sql);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Operation.Should().Be(operation);
        result.Value.Collection.Should().Be(collection);
    }

    [Theory]
    // FROM inside a function call or a projection subquery must not win over the outer table.
    [InlineData("SELECT EXTRACT(EPOCH FROM d.last_seen_at) FROM device_manager.devices AS d", "SELECT device_manager.devices")]
    [InlineData("SELECT substring(d.name FROM 1 FOR 3) FROM device_manager.devices AS d", "SELECT device_manager.devices")]
    [InlineData("SELECT d.id, (SELECT count(*)::int FROM device_manager.device_tags AS t WHERE t.device_id = d.id) FROM device_manager.devices AS d", "SELECT device_manager.devices")]
    [InlineData("SELECT * FROM (SELECT id FROM x.inner_table) AS sub", "SELECT")]
    public void Parse_NestedFrom_UsesTopLevelTarget(string sql, string expectedSummary)
    {
        // Act
        var result = SqlQuerySummary.Parse(sql);

        // Assert
        result!.Value.Summary.Should().Be(expectedSummary);
    }

    [Fact]
    public void Parse_Cte_UsesMainStatementOperationAndTarget()
    {
        // Arrange
        const string sql = """
            WITH recent AS (SELECT device_id FROM telemetry_processor.device_heartbeats WHERE ts > @p0)
            UPDATE device_manager.devices SET status = 'Online' WHERE id IN (SELECT device_id FROM recent)
            """;

        // Act
        var result = SqlQuerySummary.Parse(sql);

        // Assert
        result!.Value.Summary.Should().Be("UPDATE device_manager.devices");
    }

    [Fact]
    public void Parse_CteSelectingFromCte_ReportsCteName()
    {
        // Known limit, documented so a change in behaviour is deliberate.
        // Act
        var result = SqlQuerySummary.Parse("WITH a AS (SELECT 1 FROM t.u) SELECT * FROM a");

        // Assert
        result!.Value.Summary.Should().Be("SELECT a");
    }

    [Fact]
    public void Parse_MultiStatementBatch_UsesFirstStatement()
    {
        // Act
        var result = SqlQuerySummary.Parse("INSERT INTO a.b (x) VALUES (@p0);\nUPDATE a.c SET y = @p1;");

        // Assert
        result!.Value.Summary.Should().Be("INSERT a.b");
    }

    [Fact]
    public void Parse_CommentMarkersInsideStringLiteral_AreIgnored()
    {
        // Act
        var result = SqlQuerySummary.Parse("SELECT '--', '/*' FROM x.y");

        // Assert
        result!.Value.Summary.Should().Be("SELECT x.y");
    }

    [Fact]
    public void Parse_StringLiteralContent_NeverEndsUpInSummary()
    {
        // Act
        var result = SqlQuerySummary.Parse("SELECT 'secret FROM leaked.table' FROM x.y");

        // Assert
        result!.Value.Summary.Should().Be("SELECT x.y");
    }

    [Fact]
    public void Parse_PathologicalUnterminatedComments_CompletesQuickly()
    {
        // Arrange: many '/*' without '*/' made the previous regex-based comment stripping superlinear.
        var sql = "SELECT 1 " + string.Concat(Enumerable.Repeat("/* x ", 20_000));

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = SqlQuerySummary.Parse(sql);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(200));
        result!.Value.Summary.Should().Be("SELECT");
    }

    [Fact]
    public void Parse_HugeStatement_ScansOnlyThePrefix()
    {
        // Arrange
        var values = string.Join(", ", Enumerable.Repeat("(@p0, @p1)", 200_000));
        var sql = $"INSERT INTO telemetry_processor.device_metrics (a, b) VALUES {values}";

        // Act
        var result = SqlQuerySummary.Parse(sql);

        // Assert
        sql.Length.Should().BeGreaterThan(SqlQuerySummary.MaxScanLength);
        result!.Value.Summary.Should().Be("INSERT telemetry_processor.device_metrics");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(SELECT 1)")]
    [InlineData("FOOBAR something")]
    public void Parse_UnrecognisedInput_ReturnsNull(string? sql)
    {
        // Act
        var result = SqlQuerySummary.Parse(sql);

        // Assert
        result.Should().BeNull();
    }
}
