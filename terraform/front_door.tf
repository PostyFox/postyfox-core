locals {
  front_door_profile_name         = "${local.appname}-afd-SPA${local.hyphen-env}"
  front_door_sku_name             = "Standard_AzureFrontDoor"
  front_door_endpoint_name        = "${local.appname}${local.hyphen-env}"
  front_door_origin_group_name    = "OriginGroup"
  front_door_origin_name          = "ContainerOrigin"
  front_door_route_name           = "MyRoute"
  front_door_custom_domain_name   = "PortalDomain"
}

resource "azurerm_cdn_frontdoor_profile" "fd_profile" {
  count               = var.environment == "prod" ? 1 : 0 
  name                = local.front_door_profile_name
  resource_group_name = azurerm_resource_group.rg.name
  sku_name            = local.front_door_sku_name
}

resource "azurerm_cdn_frontdoor_endpoint" "fd_endpoint" {
  count                    = var.environment == "prod" ? 1 : 0 
  name                     = local.front_door_endpoint_name
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.fd_profile[0].id
}

resource "azurerm_cdn_frontdoor_origin_group" "fd_origin_group" {
  count                    = var.environment == "prod" ? 1 : 0 
  name                     = local.front_door_origin_group_name
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.fd_profile[0].id
  session_affinity_enabled = true

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  health_probe {
    path                = "/"
    request_type        = "HEAD"
    protocol            = "Https"
    interval_in_seconds = 100
  }
}

resource "azurerm_cdn_frontdoor_origin" "fd_container_origin" {
  count                         = var.environment == "prod" ? 1 : 0 
  name                          = local.front_door_origin_name
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.fd_origin_group[0].id

  enabled                        = true
  host_name                      = azurerm_storage_account.spa_storage.primary_web_host
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = azurerm_storage_account.spa_storage.primary_web_host
  priority                       = 1
  weight                         = 1000
  certificate_name_check_enabled = true
}

resource "azurerm_cdn_frontdoor_route" "fd_route" {
  count                         = var.environment == "prod" ? 1 : 0 
  name                          = local.front_door_route_name
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.fd_endpoint[0].id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.fd_origin_group[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.fd_container_origin[0].id]

  supported_protocols       = ["Http", "Https"]
  patterns_to_match         = ["/*"]
  forwarding_protocol       = "HttpsOnly"
  link_to_default_domain    = true
  https_redirect_enabled    = true

  cdn_frontdoor_custom_domain_ids = [
    azurerm_cdn_frontdoor_custom_domain.fd_custom_domain[0].id
   ]
}

resource "azurerm_cdn_frontdoor_custom_domain" "fd_custom_domain" {
  count                    = var.environment == "prod" ? 1 : 0 
  name                     = local.front_door_custom_domain_name
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.fd_profile[0].id
  host_name                = "${local.portal-prefix}${local.portal-address}"

  tls {
    certificate_type    = "ManagedCertificate"
    minimum_tls_version = "TLS12"
  }
}