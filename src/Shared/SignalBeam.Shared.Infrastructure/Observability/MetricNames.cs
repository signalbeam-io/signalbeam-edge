namespace SignalBeam.Shared.Infrastructure.Observability;

/// <summary>
/// Constants for OpenTelemetry metric names.
/// </summary>
public static class MetricNames
{
    /// <summary>
    /// Meter name for SignalBeam metrics.
    /// </summary>
    public const string SignalBeam = "SignalBeam";

    /// <summary>
    /// Device metrics.
    /// </summary>
    public static class Devices
    {
        public const string Total = "signalbeam.devices.total";
        public const string Online = "signalbeam.devices.online";
        public const string Offline = "signalbeam.devices.offline";
        public const string RegistrationDuration = "signalbeam.devices.registration.duration";
        public const string HeartbeatProcessingDuration = "signalbeam.devices.heartbeat.processing.duration";
    }

    /// <summary>
    /// Bundle metrics.
    /// </summary>
    public static class Bundles
    {
        public const string Total = "signalbeam.bundles.total";
        public const string Versions = "signalbeam.bundles.versions.total";
        public const string Deployments = "signalbeam.bundles.deployments.total";
        public const string DeploymentDuration = "signalbeam.bundles.deployment.duration";
        public const string DeploymentSuccessRate = "signalbeam.bundles.deployment.success_rate";
    }

    /// <summary>
    /// Message broker metrics.
    /// </summary>
    public static class Messaging
    {
        public const string MessagesPublished = "signalbeam.messaging.published.total";
        public const string MessagesReceived = "signalbeam.messaging.received.total";
        public const string MessageProcessingDuration = "signalbeam.messaging.processing.duration";
        public const string MessageProcessingErrors = "signalbeam.messaging.errors.total";
    }

    /// <summary>
    /// HTTP metrics.
    /// </summary>
    public static class Http
    {
        public const string RequestDuration = "signalbeam.http.request.duration";
        public const string RequestsTotal = "signalbeam.http.requests.total";
        public const string RequestsActive = "signalbeam.http.requests.active";
    }
}
