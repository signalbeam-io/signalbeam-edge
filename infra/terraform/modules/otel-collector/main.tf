# Deploy OpenTelemetry Collector to route traces, metrics, and logs
# from microservices to Tempo, Prometheus, and Loki.

locals {
  parsed_kubeconfig = yamldecode(var.kube_config_raw)

  kube_config = {
    host                   = local.parsed_kubeconfig.clusters[0].cluster.server
    client_certificate     = base64decode(local.parsed_kubeconfig.users[0].user.client-certificate-data)
    client_key             = base64decode(local.parsed_kubeconfig.users[0].user.client-key-data)
    cluster_ca_certificate = base64decode(local.parsed_kubeconfig.clusters[0].cluster.certificate-authority-data)
  }

  common_labels = {
    "app.kubernetes.io/managed-by" = "terraform"
    "signalbeam.io/environment"    = var.environment
    "signalbeam.io/project"        = var.project
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

resource "helm_release" "otel_collector" {
  name       = "otel-collector"
  repository = "https://open-telemetry.github.io/opentelemetry-helm-charts"
  chart      = "opentelemetry-collector"
  version    = var.chart_version
  namespace  = var.namespace

  # Deploy as Deployment (not DaemonSet) for app-level telemetry
  set {
    name  = "mode"
    value = "deployment"
  }

  set {
    name  = "replicaCount"
    value = "2"
  }

  # Resources
  set {
    name  = "resources.requests.cpu"
    value = "100m"
  }

  set {
    name  = "resources.requests.memory"
    value = "256Mi"
  }

  set {
    name  = "resources.limits.cpu"
    value = "500m"
  }

  set {
    name  = "resources.limits.memory"
    value = "512Mi"
  }

  # OTLP receiver ports
  set {
    name  = "ports.otlp.enabled"
    value = "true"
  }

  set {
    name  = "ports.otlp-http.enabled"
    value = "true"
  }

  # Prometheus metrics port for self-monitoring
  set {
    name  = "ports.prometheus.enabled"
    value = "true"
  }

  # Collector pipeline configuration
  values = [yamlencode({
    config = {
      receivers = {
        otlp = {
          protocols = {
            grpc = { endpoint = "0.0.0.0:4317" }
            http = { endpoint = "0.0.0.0:4318" }
          }
        }
      }

      processors = {
        batch = {
          timeout         = "5s"
          send_batch_size = 1024
        }
        memory_limiter = {
          check_interval         = "1s"
          limit_mib              = 400
          spike_limit_percentage = 30
        }
      }

      exporters = {
        # Traces → Tempo
        otlp = {
          endpoint = "tempo.${var.monitoring_namespace}:4317"
          tls      = { insecure = true }
        }

        # Metrics → Prometheus (remote write)
        prometheusremotewrite = {
          endpoint = "http://kube-prometheus-stack-prometheus.${var.monitoring_namespace}:9090/api/v1/write"
        }

        # Logs → Loki
        loki = {
          endpoint = "http://loki.${var.monitoring_namespace}:3100/loki/api/v1/push"
        }

        # Debug logging (minimal)
        debug = {
          verbosity = "basic"
        }
      }

      service = {
        pipelines = {
          traces = {
            receivers  = ["otlp"]
            processors = ["memory_limiter", "batch"]
            exporters  = ["otlp"]
          }
          metrics = {
            receivers  = ["otlp"]
            processors = ["memory_limiter", "batch"]
            exporters  = ["prometheusremotewrite"]
          }
          logs = {
            receivers  = ["otlp"]
            processors = ["memory_limiter", "batch"]
            exporters  = ["loki"]
          }
        }

        telemetry = {
          metrics = { address = "0.0.0.0:8888" }
        }
      }
    }
  })]

  timeout = 600
}
