using System.Diagnostics;
using OpenTelemetry;

namespace SignalBeam.ServiceDefaults.Observability;

/// <summary>
/// Fills in what Npgsql leaves out for plain SQL commands: <c>db.operation.name</c>,
/// <c>db.collection.name</c> and <c>db.query.summary</c>, and renames the span from the generic
/// "postgresql" to the summary (e.g. "SELECT device_manager.devices"), as the OpenTelemetry
/// database span-name convention recommends.
/// </summary>
/// <remarks>Must be registered before the exporter so the changes are exported.</remarks>
public sealed class DatabaseSpanProcessor : BaseProcessor<Activity>
{
    private const string QueryText = "db.query.text";

    public override void OnEnd(Activity data)
    {
        if (!string.Equals(data.Source.Name, Extensions.NpgsqlActivitySourceName, StringComparison.Ordinal)
            || data.GetTagItem("db.query.summary") is not null)
        {
            return;
        }

        var summary = SqlQuerySummary.Parse(data.GetTagItem(QueryText) as string);
        if (summary is not { } s)
        {
            return;
        }

        data.SetTag("db.operation.name", s.Operation);
        if (s.Collection is not null)
        {
            data.SetTag("db.collection.name", s.Collection);
        }

        data.SetTag("db.query.summary", s.Summary);
        data.DisplayName = s.Summary;
    }
}
