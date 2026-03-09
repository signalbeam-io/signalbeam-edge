using System.Diagnostics.Metrics;
using SignalBeam.Shared.Infrastructure.Observability;

namespace SignalBeam.DeviceManager.Host.Metrics;

/// <summary>
/// Prometheus/OpenTelemetry metrics for certificate management operations.
/// </summary>
public sealed class CertificateMetrics
{
    private readonly Counter<long> _certificatesIssued;
    private readonly Counter<long> _certificatesRenewed;
    private readonly Counter<long> _certificatesRevoked;
    private readonly Counter<long> _authenticationTotal;
    private readonly Histogram<double> _validationDuration;

    public CertificateMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MetricNames.SignalBeam);

        _certificatesIssued = meter.CreateCounter<long>(
            MetricNames.Certificates.IssuedTotal,
            "certificates",
            "Total number of certificates issued");

        _certificatesRenewed = meter.CreateCounter<long>(
            MetricNames.Certificates.RenewedTotal,
            "certificates",
            "Total number of certificates renewed");

        _certificatesRevoked = meter.CreateCounter<long>(
            MetricNames.Certificates.RevokedTotal,
            "certificates",
            "Total number of certificates revoked");

        _authenticationTotal = meter.CreateCounter<long>(
            MetricNames.Certificates.AuthenticationTotal,
            "authentications",
            "Total authentication attempts by method and result");

        _validationDuration = meter.CreateHistogram<double>(
            MetricNames.Certificates.ValidationDuration,
            "seconds",
            "Duration of certificate validation operations");
    }

    public void RecordCertificateIssued() =>
        _certificatesIssued.Add(1);

    public void RecordCertificateRenewed() =>
        _certificatesRenewed.Add(1);

    public void RecordCertificateRevoked() =>
        _certificatesRevoked.Add(1);

    public void RecordAuthentication(string method, string result) =>
        _authenticationTotal.Add(1,
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("result", result));

    public void RecordValidationDuration(double seconds) =>
        _validationDuration.Record(seconds);
}
