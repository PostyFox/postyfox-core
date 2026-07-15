resource "azurerm_log_analytics_workspace" "this" {
  name                = "postyfox-logs"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_container_app_environment" "this" {
  name                       = "postyfox-env"
  resource_group_name        = var.resource_group_name
  location                   = var.location
  log_analytics_workspace_id = azurerm_log_analytics_workspace.this.id
}

locals {
  common_env = [
    { name = "ObjectStore__ServiceUrl", value = var.object_store_service_url },
    { name = "ObjectStore__Bucket", value = var.object_store_bucket },
    { name = "RabbitMq__Host", value = var.rabbitmq_host },
    { name = "RabbitMq__User", value = var.rabbitmq_user },
    { name = "Auth__UserHeader", value = "X-Auth-Request-User" },
    { name = "Auth__Oidc__Enabled", value = "true" },
    { name = "Auth__Oidc__Issuer", value = var.auth_oidc_issuer },
    { name = "Auth__Oidc__JwksUrl", value = var.auth_oidc_jwks_url },
    { name = "Auth__Oidc__Audience", value = var.auth_oidc_audience },
    { name = "Secrets__Provider", value = var.secrets_provider },
    { name = "Secrets__BitWarden__ServerUrl", value = var.bitwarden_server_url },
    { name = "Secrets__BitWarden__OrganizationId", value = var.bitwarden_organization_id },
    { name = "Secrets__BitWarden__IdentityUrl", value = var.bitwarden_identity_url },
    { name = "OTEL_EXPORTER_OTLP_ENDPOINT", value = var.otel_endpoint },
    { name = "OTEL_EXPORTER_OTLP_PROTOCOL", value = "grpc" },
    { name = "ASPNETCORE_URLS", value = "http://+:8080" },
  ]
  common_env_norm = [for e in local.common_env : { name = e.name, value = e.value, secret_name = null }]

  # Secret-store credentials for the selected provider. Only the ones actually supplied are wired as
  # Container App secrets (ACA rejects empty secret values), so e.g. using an API key alone works.
  bitwarden_secrets = [
    { secret_name = "bitwarden-api-key", env = "Secrets__BitWarden__ApiKey", value = var.bitwarden_api_key },
    { secret_name = "bitwarden-client-id", env = "Secrets__BitWarden__ClientId", value = var.bitwarden_client_id },
    { secret_name = "bitwarden-client-secret", env = "Secrets__BitWarden__ClientSecret", value = var.bitwarden_client_secret },
  ]
  bitwarden_secrets_present = [for s in local.bitwarden_secrets : s if s.value != ""]

  secret_env = concat([
    { name = "ConnectionStrings__Postgres", secret_name = "postgres-connection" },
    { name = "NodeConnectors__InternalToken", secret_name = "internal-token" },
    { name = "ObjectStore__AccessKey", secret_name = "objectstore-access-key" },
    { name = "ObjectStore__SecretKey", secret_name = "objectstore-secret-key" },
    { name = "RabbitMq__Password", secret_name = "rabbitmq-password" },
    ],
    [for s in local.bitwarden_secrets_present : { name = s.env, secret_name = s.secret_name }],
  )
  secret_env_norm = [for e in local.secret_env : { name = e.name, value = null, secret_name = e.secret_name }]

  dotnet_env = concat(local.common_env_norm, local.secret_env_norm)

  app_secrets = concat([
    { name = "postgres-connection", value = var.postgres_connection },
    { name = "internal-token", value = var.internal_token },
    { name = "objectstore-access-key", value = var.object_store_access_key },
    { name = "objectstore-secret-key", value = var.object_store_secret_key },
    { name = "rabbitmq-password", value = var.rabbitmq_password },
    ],
    [for s in local.bitwarden_secrets_present : { name = s.secret_name, value = s.value }],
  )

  node_base_url = "https://${azurerm_container_app.connectors_node.ingress[0].fqdn}"

  use_registry_creds = var.registry_server != "" && var.registry_username != ""
}

# --- connectors-node (internal ingress) ---
resource "azurerm_container_app" "connectors_node" {
  name                         = "postyfox-connectors-node"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  secret {
    name  = "internal-token"
    value = var.internal_token
  }
  secret {
    name  = "objectstore-access-key"
    value = var.object_store_access_key
  }
  secret {
    name  = "objectstore-secret-key"
    value = var.object_store_secret_key
  }

  dynamic "registry" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = "registry-password"
    }
  }
  dynamic "secret" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      name  = "registry-password"
      value = var.registry_password
    }
  }

  template {
    min_replicas = 1
    max_replicas = 3
    container {
      name   = "connectors-node"
      image  = "${var.image_registry}/${var.image_repository}-connectors-node:${var.image_tag}"
      cpu    = 0.25
      memory = "0.5Gi"
      env {
        name  = "PORT"
        value = "8090"
      }
      env {
        name        = "INTERNAL_TOKEN"
        secret_name = "internal-token"
      }
      env {
        name  = "OBJECT_STORE_SERVICE_URL"
        value = var.object_store_service_url
      }
      env {
        name  = "OBJECT_STORE_BUCKET"
        value = var.object_store_bucket
      }
      env {
        name  = "OBJECT_STORE_FORCE_PATH_STYLE"
        value = "true"
      }
      env {
        name        = "OBJECT_STORE_ACCESS_KEY"
        secret_name = "objectstore-access-key"
      }
      env {
        name        = "OBJECT_STORE_SECRET_KEY"
        secret_name = "objectstore-secret-key"
      }
    }
  }

  ingress {
    external_enabled = false
    target_port      = 8090
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# --- core-api (internal ingress, owns migrations) ---
resource "azurerm_container_app" "core_api" {
  name                         = "postyfox-core-api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  dynamic "secret" {
    for_each = local.app_secrets
    content {
      name  = secret.value.name
      value = secret.value.value
    }
  }
  dynamic "registry" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = "registry-password"
    }
  }
  dynamic "secret" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      name  = "registry-password"
      value = var.registry_password
    }
  }

  template {
    min_replicas = 1
    max_replicas = 3
    container {
      name   = "core-api"
      image  = "${var.image_registry}/${var.image_repository}-core-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"
      dynamic "env" {
        for_each = concat(local.dotnet_env, [
          { name = "ApplyMigrations", value = "true", secret_name = null },
          { name = "SeedServiceDefinitions", value = "true", secret_name = null },
          { name = "NodeConnectors__BaseUrl", value = local.node_base_url, secret_name = null },
        ])
        content {
          name        = env.value.name
          value       = env.value.value
          secret_name = env.value.secret_name
        }
      }
    }
  }

  # Internal only. Public access goes through the OIDC edge; the APIs are never externally exposed
  # (they re-validate the forwarded JWT in-app). Provisioning the edge is a follow-up — see README.
  ingress {
    external_enabled = false
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# --- post-api (internal ingress) ---
resource "azurerm_container_app" "post_api" {
  name                         = "postyfox-post-api"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  dynamic "secret" {
    for_each = local.app_secrets
    content {
      name  = secret.value.name
      value = secret.value.value
    }
  }
  dynamic "registry" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = "registry-password"
    }
  }
  dynamic "secret" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      name  = "registry-password"
      value = var.registry_password
    }
  }

  template {
    min_replicas = 1
    max_replicas = 3
    container {
      name   = "post-api"
      image  = "${var.image_registry}/${var.image_repository}-post-api:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"
      dynamic "env" {
        for_each = local.dotnet_env
        content {
          name        = env.value.name
          value       = env.value.value
          secret_name = env.value.secret_name
        }
      }
    }
  }

  # Internal only — see core-api. The OIDC edge is the sole external Container App.
  ingress {
    external_enabled = false
    target_port      = 8080
    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }
}

# --- posting-worker (no ingress) ---
resource "azurerm_container_app" "worker" {
  name                         = "postyfox-worker"
  container_app_environment_id = azurerm_container_app_environment.this.id
  resource_group_name          = var.resource_group_name
  revision_mode                = "Single"

  dynamic "secret" {
    for_each = local.app_secrets
    content {
      name  = secret.value.name
      value = secret.value.value
    }
  }
  dynamic "registry" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      server               = var.registry_server
      username             = var.registry_username
      password_secret_name = "registry-password"
    }
  }
  dynamic "secret" {
    for_each = local.use_registry_creds ? [1] : []
    content {
      name  = "registry-password"
      value = var.registry_password
    }
  }

  template {
    min_replicas = 1
    max_replicas = 5
    container {
      name   = "worker"
      image  = "${var.image_registry}/${var.image_repository}-worker:${var.image_tag}"
      cpu    = 0.5
      memory = "1Gi"
      dynamic "env" {
        for_each = concat(local.dotnet_env, [
          { name = "NodeConnectors__BaseUrl", value = local.node_base_url, secret_name = null },
        ])
        content {
          name        = env.value.name
          value       = env.value.value
          secret_name = env.value.secret_name
        }
      }
    }
  }
}
