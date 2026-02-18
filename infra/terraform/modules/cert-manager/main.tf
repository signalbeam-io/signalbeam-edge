# Deploy cert-manager with Let's Encrypt ClusterIssuers for TLS certificate management.

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

resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  version          = var.chart_version
  namespace        = "cert-manager"
  create_namespace = true

  set {
    name  = "crds.enabled"
    value = "true"
  }

  # Workload identity for Azure DNS01 solver
  set {
    name  = "podLabels.azure\\.workload\\.identity/use"
    value = "true"
  }

  set {
    name  = "serviceAccount.labels.azure\\.workload\\.identity/use"
    value = "true"
  }

  set {
    name  = "serviceAccount.annotations.azure\\.workload\\.identity/client-id"
    value = var.workload_identity_client_id
  }

  # Prometheus monitoring
  set {
    name  = "prometheus.enabled"
    value = "true"
  }

  set {
    name  = "prometheus.servicemonitor.enabled"
    value = "true"
  }

  timeout = 600
}

# ClusterIssuer for Let's Encrypt production
resource "kubernetes_manifest" "letsencrypt_prod" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "ClusterIssuer"
    metadata = {
      name   = "letsencrypt-prod"
      labels = local.common_labels
    }
    spec = {
      acme = {
        server = "https://acme-v02.api.letsencrypt.org/directory"
        email  = var.acme_email
        privateKeySecretRef = {
          name = "letsencrypt-prod-key"
        }
        solvers = [{
          dns01 = {
            azureDNS = {
              hostedZoneName    = var.dns_zone_name
              resourceGroupName = var.dns_zone_resource_group
              subscriptionID    = "" # Uses workload identity
              environment       = "AzurePublicCloud"
              managedIdentity = {
                clientID = var.workload_identity_client_id
              }
            }
          }
        }]
      }
    }
  }

  depends_on = [helm_release.cert_manager]
}

# ClusterIssuer for Let's Encrypt staging (for testing)
resource "kubernetes_manifest" "letsencrypt_staging" {
  manifest = {
    apiVersion = "cert-manager.io/v1"
    kind       = "ClusterIssuer"
    metadata = {
      name   = "letsencrypt-staging"
      labels = local.common_labels
    }
    spec = {
      acme = {
        server = "https://acme-staging-v02.api.letsencrypt.org/directory"
        email  = var.acme_email
        privateKeySecretRef = {
          name = "letsencrypt-staging-key"
        }
        solvers = [{
          dns01 = {
            azureDNS = {
              hostedZoneName    = var.dns_zone_name
              resourceGroupName = var.dns_zone_resource_group
              subscriptionID    = ""
              environment       = "AzurePublicCloud"
              managedIdentity = {
                clientID = var.workload_identity_client_id
              }
            }
          }
        }]
      }
    }
  }

  depends_on = [helm_release.cert_manager]
}
