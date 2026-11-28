namespace SignalBeam.EdgeAgent.Application.Services;

// Labels the agent puts on every container it creates. Reconciliation only ever lists, replaces and
// stops containers carrying Managed=true, so containers it didn't create are never touched.
public static class ManagedContainerLabels
{
    public const string Managed = "signalbeam.managed";
    public const string BundleId = "signalbeam.bundle.id";
    public const string BundleVersion = "signalbeam.bundle.version";
    public const string Image = "signalbeam.image";

    public static bool IsManaged(IDictionary<string, string>? labels) =>
        labels is not null
        && labels.TryGetValue(Managed, out var value)
        && string.Equals(value, "true", StringComparison.Ordinal); // same as the Docker label filter

    public static Dictionary<string, string> For(ContainerSpec spec, ContainerOwner owner)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Managed] = "true",
            [Image] = spec.Image
        };

        if (!string.IsNullOrEmpty(owner.BundleId))
        {
            labels[BundleId] = owner.BundleId;
        }

        if (!string.IsNullOrEmpty(owner.BundleVersion))
        {
            labels[BundleVersion] = owner.BundleVersion;
        }

        return labels;
    }
}
