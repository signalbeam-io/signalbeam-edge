output "release_name" {
  description = "Helm release name"
  value       = helm_release.nats.name
}

output "namespace" {
  description = "Namespace where NATS is deployed"
  value       = helm_release.nats.namespace
}

output "client_url" {
  description = "NATS client connection URL"
  value       = "nats://nats.${helm_release.nats.namespace}:4222"
}

output "monitor_url" {
  description = "NATS monitoring endpoint URL"
  value       = "http://nats.${helm_release.nats.namespace}:8222"
}
