locals {
  location = "UK South"
  appname  = "postyfox"

  hyphen-env    = var.environment == "" ? "" : "-${var.environment}"
  portal-prefix = var.environment == "prod" ? "" : "${var.environment}."

  portal-address    = "cp.postyfox.com"
  posting-address   = "post.postyfox.com"
  mainapi-address   = "api.postyfox.com"
  nodejsapi-address = "api2.postyfox.com"
  container-app-address = "container.postyfox.com"
}