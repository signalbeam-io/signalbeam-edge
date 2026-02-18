output "namespace" {
  description = "Namespace where cert-manager is deployed"
  value       = helm_release.cert_manager.namespace
}

output "cluster_issuer_prod" {
  description = "Name of the production ClusterIssuer"
  value       = "letsencrypt-prod"
}

output "cluster_issuer_staging" {
  description = "Name of the staging ClusterIssuer"
  value       = "letsencrypt-staging"
}
