using NATS.Client.Core;

namespace SignalBeam.ServiceDefaults.Observability;

/// <summary>
/// Configures NATS.Net's built-in tracing (ActivitySource "NATS.Net", registered in ServiceDefaults).
/// </summary>
/// <remarks>
/// Without a filter, JetStream's own protocol traffic dominates the traces: every consumer fetch
/// (every few seconds, per consumer) starts new root traces for <c>$JS.API.*</c> requests and the
/// <c>_INBOX.*</c> subscriptions/replies behind them. Those carry no application behaviour, so they
/// are dropped. Application subjects, and JetStream acks (which sit inside a message's processing
/// trace), are kept. Delivered JetStream messages keep their original subject, so trace-context
/// propagation is unaffected.
/// <para>
/// Applied once by <see cref="Extensions.ConfigureOpenTelemetry"/>, next to the registration of the
/// <c>NATS.Net</c> source, so every service that traces NATS also gets the filter. Only the default
/// <c>_INBOX.</c> prefix is matched; a custom <c>NatsOpts.InboxPrefix</c> must be added here.
/// </para>
/// </remarks>
public static class NatsTracing
{
    private static readonly string[] ControlPlanePrefixes = ["$JS.API.", "_INBOX."];

    /// <summary>
    /// Applies the SignalBeam tracing filter to <see cref="NatsInstrumentationOptions.Default"/>.
    /// The options are process-wide; calling this more than once is harmless.
    /// </summary>
    public static void Configure() => NatsInstrumentationOptions.Default.Filter = ShouldTrace;

    /// <summary>Returns <c>false</c> for JetStream control-plane subjects.</summary>
    public static bool ShouldTrace(NatsInstrumentationContext context) => ShouldTrace(context.Subject);

    /// <summary>Returns <c>false</c> for JetStream control-plane subjects.</summary>
    public static bool ShouldTrace(string? subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return true;
        }

        foreach (var prefix in ControlPlanePrefixes)
        {
            if (subject.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
