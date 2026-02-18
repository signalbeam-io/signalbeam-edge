# Deploy Grafana Tempo for distributed tracing.

locals {
  parsed_kubeconfig = yamldecode(var.kube_config_raw)

  kube_config = {
    host                   = local.parsed_kubeconfig.clusters[0].cluster.server
    client_certificate     = base64decode(local.parsed_kubeconfig.users[0].user.client-certificate-data)
    client_key             = base64decode(local.parsed_kubeconfig.users[0].user.client-key-data)
    cluster_ca_certificate = base64decode(local.parsed_kubeconfig.clusters[0].cluster.certificate-authority-data)
  }
}

provider "helm" {
  kubernetes {
    host                   = local.kube_config.host
    client_certificate     = local.kube_config.client_certificate
    client_key             = local.kube_config.client_key
    cluster_ca_certificate = local.kube_config.cluster_ca_certificate
  }
}

provider "kubernetes" {
  host                   = local.kube_config.host
  client_certificate     = local.kube_config.client_certificate
  client_key             = local.kube_config.client_key
  cluster_ca_certificate = local.kube_config.cluster_ca_certificate
}

resource "helm_release" "tempo" {
  name       = "tempo"
  repository = "https://grafana.github.io/helm-charts"
  chart      = "tempo"
  version    = var.chart_version
  namespace  = var.namespace

  # OTLP gRPC receiver (used by OTEL Collector)
  set {
    name  = "tempo.receivers.otlp.protocols.grpc.endpoint"
    value = "0.0.0.0:4317"
  }

  # OTLP HTTP receiver
  set {
    name  = "tempo.receivers.otlp.protocols.http.endpoint"
    value = "0.0.0.0:4318"
  }

  # Retention
  set {
    name  = "tempo.retention"
    value = var.retention_period
  }

  # Local storage for dev
  set {
    name  = "persistence.enabled"
    value = "true"
  }

  set {
    name  = "persistence.size"
    value = var.storage_size
  }

  # Resources
  set {
    name  = "tempo.resources.requests.cpu"
    value = "100m"
  }

  set {
    name  = "tempo.resources.requests.memory"
    value = "256Mi"
  }

  set {
    name  = "tempo.resources.limits.cpu"
    value = "500m"
  }

  set {
    name  = "tempo.resources.limits.memory"
    value = "512Mi"
  }

  timeout = 600
}
