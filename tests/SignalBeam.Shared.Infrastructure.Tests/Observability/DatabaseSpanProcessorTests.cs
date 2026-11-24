using System.Diagnostics;
using FluentAssertions;
using SignalBeam.ServiceDefaults;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.Shared.Infrastructure.Tests.Observability;

public sealed class DatabaseSpanProcessorTests : IDisposable
{
    private readonly ActivitySource _npgsql = new(Extensions.NpgsqlActivitySourceName);
    private readonly ActivitySource _other = new("SignalBeam.Tests.Other");
    private readonly ActivityListener _listener;
    private readonly DatabaseSpanProcessor _processor = new();

    public DatabaseSpanProcessorTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void OnEnd_NpgsqlSpan_SetsSemanticTagsAndRenamesSpan()
    {
        using var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "SELECT d.id FROM device_manager.devices AS d");

        _processor.OnEnd(activity);

        activity.DisplayName.Should().Be("SELECT device_manager.devices");
        activity.GetTagItem("db.operation.name").Should().Be("SELECT");
        activity.GetTagItem("db.collection.name").Should().Be("device_manager.devices");
        activity.GetTagItem("db.query.summary").Should().Be("SELECT device_manager.devices");
    }

    [Fact]
    public void OnEnd_SpanFromOtherSource_IsUntouched()
    {
        using var activity = _other.StartActivity("postgresql")!;
        activity.SetTag("db.query.text", "SELECT 1 FROM x.y");

        _processor.OnEnd(activity);

        activity.DisplayName.Should().Be("postgresql");
        activity.GetTagItem("db.operation.name").Should().BeNull();
    }

    [Fact]
    public void OnEnd_UnparseableQuery_KeepsOriginalName()
    {
        using var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "(SELECT 1)");

        _processor.OnEnd(activity);

        activity.DisplayName.Should().Be("postgresql");
        activity.GetTagItem("db.query.summary").Should().BeNull();
    }

    [Fact]
    public void OnEnd_ExistingSummary_IsNotOverwritten()
    {
        using var activity = _npgsql.StartActivity("custom name", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "SELECT 1 FROM x.y");
        activity.SetTag("db.query.summary", "custom summary");

        _processor.OnEnd(activity);

        activity.DisplayName.Should().Be("custom name");
        activity.GetTagItem("db.query.summary").Should().Be("custom summary");
    }

    public void Dispose()
    {
        _listener.Dispose();
        _npgsql.Dispose();
        _other.Dispose();
        _processor.Dispose();
    }
}
