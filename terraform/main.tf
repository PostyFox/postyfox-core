terraform {
  required_version = ">= 1.3.0, < 2.0.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.77.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "terraform-rg"
    storage_account_name = "stterraformpfox"
    container_name       = "tfstate"
    key                  = "dev-terraform.tfstate"
  }
}

provider "azurerm" {
  features {}

  # If you are deploying this onto your own tenant, you will want to change this
  subscription_id = "b568099c-fb2f-4e51-946f-d9e40f80e73b"
}