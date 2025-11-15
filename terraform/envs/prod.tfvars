environment="prod"

# OIDC Authentication Configuration
# Replace these values with your OIDC provider details (Keycloak, Auth0, Okta, etc.)
oidc_client_id="YOUR_OIDC_CLIENT_ID"
oidc_issuer="https://auth.postyfox.com/realms/postyfox"
openid_configuration_endpoint="https://auth.postyfox.com/realms/postyfox/.well-known/openid-configuration"

kv_logs = ["AuditEvent"]

cors = ["https://cp.postyfox.com"]

twitchClientId    = "kuzdmn0w740xkkyuteg5yt6o0fybrq"
twitchCallbackUrl = "https://post.postyfox.com/api/Twitch_SubscriptionCallBack"

# Container App Configuration
container_registry_url              = "ghcr.io"
container_registry_username         = "postyfox"
# Note: container_registry_password is stored in Key Vault as "ContainerRegistryPassword"
container_image_name                = "postyfox/postyfox-frontend"
container_image_tag                 = "latest"
container_app_custom_domain_enabled = true
container_app_logs                  = ["ContainerAppConsoleLogs", "ContainerAppSystemLogs"]