locals {
  location = "UK South"
  appname  = "postyfox"

  hyphen-env = var.environment == "" ? "" : "-${var.environment}"
  portal-prefix = var.environment == "" ? "" : "${var.environment}."

  portal-address = "cp.postyfox.com"
}