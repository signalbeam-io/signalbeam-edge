output "namespace" {
  description = "Namespace where ingress-nginx is deployed"
  value       = helm_release.ingress_nginx.namespace
}

output "ingress_class" {
  description = "IngressClass name"
  value       = "nginx"
}
