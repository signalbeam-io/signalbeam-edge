namespace SignalBeam.TrafficGenerator;

// What an operator does in the web UI, weighted roughly by how often: mostly reads, some
// changes, and a few deliberately invalid requests so 4xx flows show up in traces too.
internal enum OperatorAction
{
    ListDevices,
    FilterDevices,
    GetDevice,
    GetDeviceHealth,
    GetDeviceActivity,
    ListGroups,
    GetGroupMembers,
    ListTags,
    SearchDevicesByTag,
    AddDeviceTag,
    RemoveDeviceTag,
    ListBundles,
    GetBundle,
    CreateBundle,
    CreateBundleVersion,
    AssignBundle,
    ListRollouts,
    GetActiveRollouts,
    CreateRollout,
    StartRollout,
    PauseRollout,
    ResumeRollout,
    RollbackRollout,
    ListAlerts,
    GetAlertStatistics,
    AcknowledgeAlert,
    ResolveAlert,
    InvalidRequest
}

internal static class OperatorActions
{
    public static readonly IReadOnlyList<(OperatorAction Action, int Weight)> Weights =
    [
        (OperatorAction.ListDevices, 14),
        (OperatorAction.FilterDevices, 8),
        (OperatorAction.GetDevice, 10),
        (OperatorAction.GetDeviceHealth, 6),
        (OperatorAction.GetDeviceActivity, 3),
        (OperatorAction.ListGroups, 5),
        (OperatorAction.GetGroupMembers, 3),
        (OperatorAction.ListTags, 3),
        (OperatorAction.SearchDevicesByTag, 3),
        (OperatorAction.AddDeviceTag, 2),
        (OperatorAction.RemoveDeviceTag, 2),
        (OperatorAction.ListBundles, 5),
        (OperatorAction.GetBundle, 3),
        (OperatorAction.CreateBundle, 1),
        (OperatorAction.CreateBundleVersion, 2),
        (OperatorAction.AssignBundle, 2),
        (OperatorAction.ListRollouts, 4),
        (OperatorAction.GetActiveRollouts, 2),
        (OperatorAction.CreateRollout, 1),
        (OperatorAction.StartRollout, 1),
        (OperatorAction.PauseRollout, 1),
        (OperatorAction.ResumeRollout, 1),
        (OperatorAction.RollbackRollout, 1),
        (OperatorAction.ListAlerts, 6),
        (OperatorAction.GetAlertStatistics, 3),
        (OperatorAction.AcknowledgeAlert, 1),
        (OperatorAction.ResolveAlert, 1)
    ];

    public static string Name(OperatorAction action) => action switch
    {
        OperatorAction.InvalidRequest => "invalid-request",
        _ => string.Concat(action.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "-" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()))
    };
}

// Picks actions from the weighted mix. The same seed gives the same sequence.
internal sealed class ActionPicker
{
    private readonly Random _random;
    private readonly double _invalidRate;
    private readonly int _totalWeight;

    public ActionPicker(int seed, double invalidRate)
    {
        _random = new Random(seed);
        _invalidRate = invalidRate;
        _totalWeight = OperatorActions.Weights.Sum(w => w.Weight);
    }

    // Shared with the actions so their inputs (which device, which filter) are seeded too.
    public Random Random => _random;

    public OperatorAction Next()
    {
        if (_random.NextDouble() < _invalidRate)
        {
            return OperatorAction.InvalidRequest;
        }

        var roll = _random.Next(_totalWeight);
        foreach (var (action, weight) in OperatorActions.Weights)
        {
            if (roll < weight)
            {
                return action;
            }

            roll -= weight;
        }

        return OperatorActions.Weights[^1].Action;
    }
}
