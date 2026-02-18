# Deploy NATS with JetStream to AKS using the official Helm chart.

provider "helm" {
  kubernetes {
    host                   = local.kube_config.host
    client_certificate     = base64decode(local.kube_config.client_certificate)
    client_key             = base64decode(local.kube_config.client_key)
    cluster_ca_certificate = base64decode(local.kube_config.cluster_ca_certificate)
  }
}

provider "kubernetes" {
  host                   = local.kube_config.host
  client_certificate     = base64decode(local.kube_config.client_certificate)
  client_key             = base64decode(local.kube_config.client_key)
  cluster_ca_certificate = base64decode(local.kube_config.cluster_ca_certificate)
}

locals {
  kube_config = yamldecode(var.kube_config_raw).clusters[0].cluster != null ? {
    host                   = yamldecode(var.kube_config_raw).clusters[0].cluster.server
    client_certificate     = yamldecode(var.kube_config_raw).users[0].user.client-certificate-data
    client_key             = yamldecode(var.kube_config_raw).users[0].user.client-key-data
    cluster_ca_certificate = yamldecode(var.kube_config_raw).clusters[0].cluster.certificate-authority-data
  } : {}
}

resource "kubernetes_namespace" "nats" {
  metadata {
    name = var.namespace
    labels = {
      "app.kubernetes.io/managed-by" = "terraform"
    }
  }

  lifecycle {
    ignore_changes = [metadata[0].labels, metadata[0].annotations]
  }
}

resource "helm_release" "nats" {
  name       = "nats"
  repository = "https://nats-io.github.io/k8s/helm/charts/"
  chart      = "nats"
  version    = var.chart_version
  namespace  = kubernetes_namespace.nats.metadata[0].name

  # Clustering
  set {
    name  = "config.cluster.enabled"
    value = "true"
  }

  set {
    name  = "config.cluster.replicas"
    value = tostring(var.cluster_replicas)
  }

  # JetStream
  set {
    name  = "config.jetstream.enabled"
    value = "true"
  }

  set {
    name  = "config.jetstream.fileStore.pvc.size"
    value = var.jetstream_storage_size
  }

  set {
    name  = "config.jetstream.memoryStore.maxSize"
    value = var.jetstream_memory_storage_size
  }

  # Resources
  set {
    name  = "container.merge.resources.requests.cpu"
    value = var.resources_requests_cpu
  }

  set {
    name  = "container.merge.resources.requests.memory"
    value = var.resources_requests_memory
  }

  set {
    name  = "container.merge.resources.limits.cpu"
    value = var.resources_limits_cpu
  }

  set {
    name  = "container.merge.resources.limits.memory"
    value = var.resources_limits_memory
  }

  # Monitoring port (8222)
  set {
    name  = "config.monitor.enabled"
    value = "true"
  }

  # Prometheus exporter
  set {
    name  = "promExporter.enabled"
    value = tostring(var.prometheus_enabled)
  }

  set {
    name  = "promExporter.podMonitor.enabled"
    value = tostring(var.prometheus_enabled)
  }

  # Pod disruption budget for HA
  set {
    name  = "podDisruptionBudget.minAvailable"
    value = "2"
  }

  timeout = 600

  depends_on = [kubernetes_namespace.nats]
}

# Post-install job to create JetStream streams
resource "kubernetes_job" "nats_stream_init" {
  metadata {
    name      = "nats-stream-init"
    namespace = kubernetes_namespace.nats.metadata[0].name
    labels = {
      "app.kubernetes.io/name"       = "nats-stream-init"
      "app.kubernetes.io/managed-by" = "terraform"
    }
  }

  spec {
    backoff_limit = 5
    template {
      metadata {
        labels = {
          "app.kubernetes.io/name" = "nats-stream-init"
        }
      }
      spec {
        restart_policy = "OnFailure"
        container {
          name  = "nats-stream-init"
          image = "natsio/nats-box:0.14.5"
          command = ["/bin/sh", "-c", <<-EOT
            set -e
            NATS_URL="nats://nats.${var.namespace}:4222"

            echo "Waiting for NATS to be ready..."
            until nats --server="$NATS_URL" server ping --count=1 2>/dev/null; do
              echo "NATS not ready, retrying in 5s..."
              sleep 5
            done

            echo "Creating DEVICE_EVENTS stream..."
            nats --server="$NATS_URL" stream add DEVICE_EVENTS \
              --subjects="signalbeam.devices.events.>" \
              --storage=file \
              --replicas=${var.cluster_replicas} \
              --retention=limits \
              --max-age=30d \
              --max-bytes=5368709120 \
              --discard=old \
              --dupe-window=2m \
              --no-allow-rollup \
              --deny-delete \
              --deny-purge \
              --defaults 2>/dev/null || echo "Stream DEVICE_EVENTS already exists"

            echo "Creating BUNDLE_ASSIGNMENTS stream..."
            nats --server="$NATS_URL" stream add BUNDLE_ASSIGNMENTS \
              --subjects="signalbeam.bundles.assignments.>,signalbeam.bundles.rollouts.>" \
              --storage=file \
              --replicas=${var.cluster_replicas} \
              --retention=limits \
              --max-age=7d \
              --max-bytes=1073741824 \
              --discard=old \
              --dupe-window=2m \
              --no-allow-rollup \
              --deny-delete \
              --deny-purge \
              --defaults 2>/dev/null || echo "Stream BUNDLE_ASSIGNMENTS already exists"

            echo "Creating TELEMETRY_METRICS stream..."
            nats --server="$NATS_URL" stream add TELEMETRY_METRICS \
              --subjects="signalbeam.telemetry.metrics.>" \
              --storage=file \
              --replicas=${var.cluster_replicas} \
              --retention=limits \
              --max-age=3d \
              --max-bytes=5368709120 \
              --discard=old \
              --dupe-window=30s \
              --no-allow-rollup \
              --deny-delete \
              --deny-purge \
              --defaults 2>/dev/null || echo "Stream TELEMETRY_METRICS already exists"

            echo "Creating DEVICE_STATUS stream..."
            nats --server="$NATS_URL" stream add DEVICE_STATUS \
              --subjects="signalbeam.devices.status.>" \
              --storage=file \
              --replicas=${var.cluster_replicas} \
              --retention=limits \
              --max-age=7d \
              --max-bytes=1073741824 \
              --discard=old \
              --dupe-window=2m \
              --no-allow-rollup \
              --deny-delete \
              --deny-purge \
              --defaults 2>/dev/null || echo "Stream DEVICE_STATUS already exists"

            echo "All streams created successfully."
            nats --server="$NATS_URL" stream ls
          EOT
          ]
        }
      }
    }
  }

  wait_for_completion = true

  timeouts {
    create = "5m"
  }

  depends_on = [helm_release.nats]
}
