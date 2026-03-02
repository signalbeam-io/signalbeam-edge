output "namespace" {
  description = "Namespace where the OTEL Collector is deployed"
  value       = helm_release.otel_collector.namespace
}

output "otlp_grpc_endpoint" {
  description = "OTEL Collector OTLP gRPC endpoint"
  value       = "http://otel-collector.${helm_release.otel_collector.namespace}:4317"
}

output "otlp_http_endpoint" {
  description = "OTEL Collector OTLP HTTP endpoint"
  value       = "http://otel-collector.${helm_release.otel_collector.namespace}:4318"
}
