namespace SignalBeam.Shared.Infrastructure.Observability;

/// <summary>
/// Constants for OpenTelemetry activity/span names.
/// </summary>
public static class ActivityNames
{
    /// <summary>
    /// Activity source name for SignalBeam services.
    /// </summary>
    public const string SignalBeam = "SignalBeam";

    /// <summary>
    /// Device-related operations.
    /// </summary>
    public static class Device
    {
        public const string Register = "Device.Register";
        public const string UpdateHeartbeat = "Device.UpdateHeartbeat";
        public const string AssignBundle = "Device.AssignBundle";
        public const string GetStatus = "Device.GetStatus";
    }

    /// <summary>
    /// Bundle-related operations.
    /// </summary>
    public static class Bundle
    {
        public const string Create = "Bundle.Create";
        public const string CreateVersion = "Bundle.CreateVersion";
        public const string Assign = "Bundle.Assign";
        public const string Deploy = "Bundle.Deploy";
    }

    /// <summary>
    /// Telemetry-related operations.
    /// </summary>
    public static class Telemetry
    {
        public const string ProcessHeartbeat = "Telemetry.ProcessHeartbeat";
        public const string ProcessMetrics = "Telemetry.ProcessMetrics";
        public const string AggregateMetrics = "Telemetry.AggregateMetrics";
    }

    /// <summary>
    /// Message broker operations.
    /// </summary>
    public static class Messaging
    {
        public const string Publish = "Messaging.Publish";
        public const string Subscribe = "Messaging.Subscribe";
        public const string ProcessMessage = "Messaging.ProcessMessage";
    }

    /// <summary>
    /// Database operations.
    /// </summary>
    public static class Database
    {
        public const string Query = "Database.Query";
        public const string Command = "Database.Command";
        public const string Transaction = "Database.Transaction";
    }
}
