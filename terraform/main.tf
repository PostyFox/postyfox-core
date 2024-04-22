terraform {
  required_version = ">= 1.3.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.100.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "terraform-rg"
    storage_account_name = "stterraformpfox"
    container_name       = "tfstate"
    key                  = "dev-terraform.tfstate"
    subscription_id      = "d0cf8868-6b53-43a4-bafe-dda6264f06de" # State account and container is in a different subscription
    use_azuread_auth     = true
    use_oidc             = true
  }
}

provider "azurerm" {
  use_oidc            = true
  storage_use_azuread = true
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
  }
}