# Deploy Grafana Loki for log aggregation in single-binary mode (dev/small clusters).

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

resource "helm_release" "loki" {
  name       = "loki"
  repository = "https://grafana.github.io/helm-charts"
  chart      = "loki"
  version    = var.chart_version
  namespace  = var.namespace

  # Single-binary mode for dev/small clusters
  set {
    name  = "deploymentMode"
    value = "SingleBinary"
  }

  set {
    name  = "singleBinary.replicas"
    value = "1"
  }

  # Disable distributed components
  set {
    name  = "read.replicas"
    value = "0"
  }

  set {
    name  = "write.replicas"
    value = "0"
  }

  set {
    name  = "backend.replicas"
    value = "0"
  }

  # Filesystem storage (no object store for dev)
  set {
    name  = "loki.storage.type"
    value = "filesystem"
  }

  set {
    name  = "loki.commonConfig.replication_factor"
    value = "1"
  }

  set {
    name  = "loki.schemaConfig.configs[0].from"
    value = "2024-01-01"
  }

  set {
    name  = "loki.schemaConfig.configs[0].store"
    value = "tsdb"
  }

  set {
    name  = "loki.schemaConfig.configs[0].object_store"
    value = "filesystem"
  }

  set {
    name  = "loki.schemaConfig.configs[0].schema"
    value = "v13"
  }

  set {
    name  = "loki.schemaConfig.configs[0].index.prefix"
    value = "loki_index_"
  }

  set {
    name  = "loki.schemaConfig.configs[0].index.period"
    value = "24h"
  }

  # Retention
  set {
    name  = "loki.limits_config.retention_period"
    value = var.retention_period
  }

  # Resources
  set {
    name  = "singleBinary.resources.requests.cpu"
    value = "100m"
  }

  set {
    name  = "singleBinary.resources.requests.memory"
    value = "256Mi"
  }

  set {
    name  = "singleBinary.resources.limits.cpu"
    value = "500m"
  }

  set {
    name  = "singleBinary.resources.limits.memory"
    value = "512Mi"
  }

  # Persistence
  set {
    name  = "singleBinary.persistence.size"
    value = var.storage_size
  }

  # Disable gateway for internal-only access
  set {
    name  = "gateway.enabled"
    value = "false"
  }

  # Disable self-monitoring to reduce overhead
  set {
    name  = "monitoring.selfMonitoring.enabled"
    value = "false"
  }

  set {
    name  = "monitoring.lokiCanary.enabled"
    value = "false"
  }

  set {
    name  = "test.enabled"
    value = "false"
  }

  timeout = 600
}
