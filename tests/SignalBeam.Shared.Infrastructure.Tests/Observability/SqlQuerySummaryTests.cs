using FluentAssertions;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.Shared.Infrastructure.Tests.Observability;

public class SqlQuerySummaryTests
{
    [Theory]
    // Shapes taken from real EF Core / Dapper traces of DeviceManager and BundleOrchestrator.
    [InlineData("SELECT count(*)::int\nFROM device_manager.devices AS d", "SELECT", "device_manager.devices")]
    [InlineData("SELECT d.id, d.name\nFROM device_manager.devices AS d\nWHERE d.tenant_id = @__tenantId_0", "SELECT", "device_manager.devices")]
    [InlineData("INSERT INTO bundle_orchestrator.\"Rollouts\" (id, name) VALUES (@p0, @p1)", "INSERT", "bundle_orchestrator.Rollouts")]
    [InlineData("UPDATE device_manager.devices SET last_seen_at = @p0 WHERE id = @p1", "UPDATE", "device_manager.devices")]
    [InlineData("DELETE FROM device_manager.devices AS d WHERE d.status = 'Pending'", "DELETE", "device_manager.devices")]
    [InlineData("select * from \"telemetry_processor\".\"device_heartbeats\"", "SELECT", "telemetry_processor.device_heartbeats")]
    [InlineData("  -- comment\n/* block */ SELECT 1", "SELECT", null)]
    [InlineData("TRUNCATE TABLE ONLY telemetry_processor.device_metrics", "TRUNCATE", "telemetry_processor.device_metrics")]
    public void Parse_ReturnsOperationAndCollection(string sql, string operation, string? collection)
    {
        var result = SqlQuerySummary.Parse(sql);

        result.Should().NotBeNull();
        result!.Value.Operation.Should().Be(operation);
        result.Value.Collection.Should().Be(collection);
    }

    [Fact]
    public void Parse_Cte_UsesMainStatementOperationAndTarget()
    {
        const string sql = """
            WITH recent AS (SELECT device_id FROM telemetry_processor.device_heartbeats WHERE ts > @p0)
            UPDATE device_manager.devices SET status = 'Online' WHERE id IN (SELECT device_id FROM recent)
            """;

        var result = SqlQuerySummary.Parse(sql);

        result!.Value.Summary.Should().Be("UPDATE device_manager.devices");
    }

    [Fact]
    public void Parse_MultiStatementBatch_UsesFirstStatement()
    {
        var result = SqlQuerySummary.Parse("INSERT INTO a.b (x) VALUES (@p0);\nUPDATE a.c SET y = @p1;");

        result!.Value.Summary.Should().Be("INSERT a.b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("(SELECT 1)")]
    [InlineData("FOOBAR something")]
    public void Parse_UnrecognisedInput_ReturnsNull(string? sql)
    {
        SqlQuerySummary.Parse(sql).Should().BeNull();
    }
}
