output "namespace" {
  description = "Namespace where the monitoring stack is deployed"
  value       = helm_release.kube_prometheus_stack.namespace
}

output "prometheus_url" {
  description = "Prometheus internal URL"
  value       = "http://kube-prometheus-stack-prometheus.${helm_release.kube_prometheus_stack.namespace}:9090"
}

output "grafana_url" {
  description = "Grafana internal URL"
  value       = "http://kube-prometheus-stack-grafana.${helm_release.kube_prometheus_stack.namespace}"
}
