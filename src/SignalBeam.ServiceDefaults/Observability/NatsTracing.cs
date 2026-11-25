using NATS.Client.Core;

namespace SignalBeam.ServiceDefaults.Observability;

// Drops JetStream control-plane spans ($JS.API.* fetches, _INBOX.* replies), which otherwise
// dominate traces. Acks and application subjects are kept. Only the default inbox prefix is matched.
public static class NatsTracing
{
    private static readonly string[] ControlPlanePrefixes = ["$JS.API.", "_INBOX."];

    // Process-wide and idempotent; called from ConfigureOpenTelemetry.
    public static void Configure() => NatsInstrumentationOptions.Default.Filter = ShouldTrace;

    public static bool ShouldTrace(NatsInstrumentationContext context) => ShouldTrace(context.Subject);

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
