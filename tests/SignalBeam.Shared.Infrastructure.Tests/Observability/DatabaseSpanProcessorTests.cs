using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using SignalBeam.ServiceDefaults;
using SignalBeam.ServiceDefaults.Observability;

namespace SignalBeam.Shared.Infrastructure.Tests.Observability;

public sealed class DatabaseSpanProcessorTests : IDisposable
{
    private const string OtherSourceName = "SignalBeam.Tests.DatabaseSpanProcessor.Other";

    private readonly ActivitySource _npgsql = new(Extensions.NpgsqlActivitySourceName);
    private readonly ActivitySource _other = new(OtherSourceName);
    private readonly ActivityListener _listener;
    private readonly DatabaseSpanProcessor _processor = new();

    public DatabaseSpanProcessorTests()
    {
        // Only listen to the two sources under test, so other activities in the test run stay off.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is Extensions.NpgsqlActivitySourceName or OtherSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void OnEnd_NpgsqlSpan_SetsSemanticTagsAndRenamesSpan()
    {
        // Arrange
        using var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "SELECT d.id FROM device_manager.devices AS d");

        // Act
        _processor.OnEnd(activity);

        // Assert
        activity.DisplayName.Should().Be("SELECT device_manager.devices");
        activity.GetTagItem("db.operation.name").Should().Be("SELECT");
        activity.GetTagItem("db.collection.name").Should().Be("device_manager.devices");
        activity.GetTagItem("db.query.summary").Should().Be("SELECT device_manager.devices");
    }

    [Fact]
    public void OnEnd_SpanFromOtherSource_IsUntouched()
    {
        // Arrange
        using var activity = _other.StartActivity("postgresql")!;
        activity.SetTag("db.query.text", "SELECT 1 FROM x.y");

        // Act
        _processor.OnEnd(activity);

        // Assert
        activity.DisplayName.Should().Be("postgresql");
        activity.GetTagItem("db.operation.name").Should().BeNull();
    }

    [Fact]
    public void OnEnd_UnparseableQuery_KeepsOriginalName()
    {
        // Arrange
        using var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "(SELECT 1)");

        // Act
        _processor.OnEnd(activity);

        // Assert
        activity.DisplayName.Should().Be("postgresql");
        activity.GetTagItem("db.query.summary").Should().BeNull();
    }

    [Fact]
    public void OnEnd_ExistingSummary_IsNotOverwritten()
    {
        // Arrange
        using var activity = _npgsql.StartActivity("custom name", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "SELECT 1 FROM x.y");
        activity.SetTag("db.query.summary", "custom summary");

        // Act
        _processor.OnEnd(activity);

        // Assert
        activity.DisplayName.Should().Be("custom name");
        activity.GetTagItem("db.query.summary").Should().Be("custom summary");
    }

    [Fact]
    public void OnEnd_PathologicalQueryText_NeverThrows()
    {
        // Arrange: OnEnd runs inside the application's DB call, so it must never throw.
        using var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client)!;
        activity.SetTag("db.query.text", "SELECT 1 " + string.Concat(Enumerable.Repeat("/* (( '' \"", 50_000)));

        // Act
        var act = () => _processor.OnEnd(activity);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureOpenTelemetry_ProcessorsRegisteredLater_SeeEnrichedSpan()
    {
        // Arrange: the OTLP exporter is registered after ConfigureOpenTelemetry's tracing setup.
        // A capturing processor registered the same way must observe the enriched span.
        var captured = new List<(string Name, object? Operation)>();
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureOpenTelemetry();
        builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddProcessor(new CapturingProcessor(captured)));
        using var host = builder.Build();
        _ = host.Services.GetRequiredService<TracerProvider>();

        // Act
        using (var activity = _npgsql.StartActivity("postgresql", ActivityKind.Client))
        {
            activity!.SetTag("db.query.text", "UPDATE device_manager.devices SET status = @p0");
        }

        // Assert
        captured.Should().ContainSingle()
            .Which.Should().Be(("UPDATE device_manager.devices", (object?)"UPDATE"));
    }

    public void Dispose()
    {
        _listener.Dispose();
        _npgsql.Dispose();
        _other.Dispose();
        _processor.Dispose();
    }

    private sealed class CapturingProcessor(List<(string Name, object? Operation)> captured) : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity data)
        {
            if (data.Source.Name == Extensions.NpgsqlActivitySourceName)
            {
                captured.Add((data.DisplayName, data.GetTagItem("db.operation.name")));
            }
        }
    }
}
