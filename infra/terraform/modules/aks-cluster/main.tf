data "azurerm_client_config" "current" {}

locals {
  location_short = "weu"
  cluster_name   = "${var.project}-aks-${var.environment}-${local.location_short}"
}

resource "azurerm_kubernetes_cluster" "this" {
  name                = local.cluster_name
  location            = var.location
  resource_group_name = var.resource_group_name
  dns_prefix          = "${var.project}-aks-${var.environment}"
  kubernetes_version  = var.kubernetes_version

  sku_tier               = "Free"
  local_account_disabled = true

  azure_active_directory_role_based_access_control {
    azure_rbac_enabled = true
    tenant_id          = data.azurerm_client_config.current.tenant_id
  }

  api_server_access_profile {
    authorized_ip_ranges = var.api_server_authorized_ip_ranges
  }

  default_node_pool {
    name                 = "system"
    vm_size              = var.vm_size
    auto_scaling_enabled = true
    min_count            = var.min_node_count
    max_count            = var.max_node_count
    vnet_subnet_id       = var.aks_subnet_id
    os_disk_size_gb      = 64

    upgrade_settings {
      max_surge = "1"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  # Azure CNI networking
  network_profile {
    network_plugin = "azure"
    network_policy = "calico"
    service_cidr   = var.service_cidr
    dns_service_ip = var.dns_service_ip
  }

  # OIDC issuer + workload identity
  oidc_issuer_enabled       = true
  workload_identity_enabled = true

  # OMS agent for Container Insights
  oms_agent {
    log_analytics_workspace_id = var.log_analytics_workspace_id
  }

  # Key Vault secrets provider
  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  # Auto-upgrade: patch channel
  automatic_upgrade_channel = "patch"

  maintenance_window {
    allowed {
      day   = "Saturday"
      hours = [2, 6]
    }
  }

  tags = var.tags
}

# --- AcrPull role for kubelet identity ---

resource "azurerm_role_assignment" "acr_pull" {
  scope                = var.container_registry_id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_kubernetes_cluster.this.kubelet_identity[0].object_id
}

# --- Federated credentials for workload identity ---

resource "azurerm_federated_identity_credential" "workload_sa" {
  name                = "fc-${var.workload_sa_namespace}-${var.workload_sa_name}"
  resource_group_name = var.resource_group_name
  parent_id           = var.workload_identity_id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.this.oidc_issuer_url
  subject             = "system:serviceaccount:${var.workload_sa_namespace}:${var.workload_sa_name}"
}

resource "azurerm_federated_identity_credential" "cert_manager" {
  name                = "fc-${var.cert_manager_namespace}-${var.cert_manager_sa_name}"
  resource_group_name = var.resource_group_name
  parent_id           = var.workload_identity_id
  audience            = ["api://AzureADTokenExchange"]
  issuer              = azurerm_kubernetes_cluster.this.oidc_issuer_url
  subject             = "system:serviceaccount:${var.cert_manager_namespace}:${var.cert_manager_sa_name}"
}
