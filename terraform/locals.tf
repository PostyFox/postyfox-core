locals {
  location = "UK South"
  appname  = "postyfox"

  hyphen-env = var.environment == "" ? "" : "-${var.environment}"
}