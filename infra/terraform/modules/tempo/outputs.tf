output "namespace" {
  description = "Namespace where Tempo is deployed"
  value       = helm_release.tempo.namespace
}

output "otlp_grpc_url" {
  description = "Tempo OTLP gRPC endpoint for trace ingestion"
  value       = "tempo.${helm_release.tempo.namespace}:4317"
}

output "query_url" {
  description = "Tempo query URL (Grafana datasource)"
  value       = "http://tempo.${helm_release.tempo.namespace}:3100"
}
