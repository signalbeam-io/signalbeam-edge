using System.Diagnostics;
using System.Text.RegularExpressions;
using OpenTelemetry;

namespace SignalBeam.ServiceDefaults.Observability;

/// <summary>
/// Fills in what Npgsql leaves out for plain SQL commands: <c>db.operation.name</c>,
/// <c>db.collection.name</c> and <c>db.query.summary</c>, and renames the span from the generic
/// "postgresql" to the summary (e.g. "SELECT device_manager.devices"), as the OpenTelemetry
/// database span-name convention recommends.
/// </summary>
/// <remarks>
/// <para>Must be registered before the exporter so the changes are exported.</para>
/// <para>
/// <see cref="OnEnd"/> runs synchronously when Npgsql stops its command activity, inside the
/// application's database call, and OpenTelemetry does not swallow processor exceptions. It therefore
/// never throws: if summarising fails, the span is exported unchanged.
/// </para>
/// </remarks>
public sealed class DatabaseSpanProcessor : BaseProcessor<Activity>
{
    private const string QueryText = "db.query.text";
    private const string QuerySummary = "db.query.summary";

    public override void OnEnd(Activity data)
    {
        if (!string.Equals(data.Source.Name, Extensions.NpgsqlActivitySourceName, StringComparison.Ordinal)
            || data.GetTagItem(QuerySummary) is not null)
        {
            return;
        }

        SqlQuerySummary.SqlSummary? parsed;
        try
        {
            parsed = SqlQuerySummary.Parse(data.GetTagItem(QueryText) as string);
        }
        catch (RegexMatchTimeoutException)
        {
            // Telemetry must never fail the database call; keep the span as Npgsql produced it.
            return;
        }

        if (parsed is not { } summary)
        {
            return;
        }

        data.SetTag("db.operation.name", summary.Operation);
        if (summary.Collection is not null)
        {
            data.SetTag("db.collection.name", summary.Collection);
        }

        data.SetTag(QuerySummary, summary.Summary);
        data.DisplayName = summary.Summary;
    }
}
