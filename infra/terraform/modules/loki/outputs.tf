output "namespace" {
  description = "Namespace where Loki is deployed"
  value       = helm_release.loki.namespace
}

output "url" {
  description = "Loki internal push URL"
  value       = "http://loki.${helm_release.loki.namespace}:3100"
}
