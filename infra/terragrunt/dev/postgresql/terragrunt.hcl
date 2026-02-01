include "root" {
  path = find_in_parent_folders()
}

terraform {
  source = "../../../terraform/modules/postgresql"
}

dependency "resource_group" {
  config_path = "../resource-group"
}

dependency "networking" {
  config_path = "../networking"
}

dependency "key_vault" {
  config_path = "../key-vault"
}

inputs = {
  resource_group_name    = dependency.resource_group.outputs.name
  delegated_subnet_id    = dependency.networking.outputs.postgresql_subnet_id
  private_dns_zone_id    = dependency.networking.outputs.postgresql_private_dns_zone_id
  administrator_password = dependency.key_vault.outputs.postgresql_admin_password
}
