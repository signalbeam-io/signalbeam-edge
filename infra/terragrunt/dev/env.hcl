# Environment-specific variables for the dev environment.
# Update subscription_id with your Azure subscription before running.

locals {
  environment     = "dev"
  location        = "westeurope"
  project         = "sb"
  subscription_id = "YOUR_SUBSCRIPTION_ID_HERE" # TODO: replace with your Azure subscription ID
}
