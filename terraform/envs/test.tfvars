environment="test"

# OIDC Authentication Configuration
# Replace these values with your OIDC provider details (Keycloak, Auth0, Okta, etc.)
oidc_client_id="9b930c86-ea5b-40d0-a200-36a152032910"
oidc_issuer="https://auth.postyfox.com/realms/PostyFox"
openid_configuration_endpoint="https://auth.postyfox.com/realms/PostyFox/.well-known/openid-configuration"

kv_logs = ["AuditEvent"]

cors = ["https://test.cp.postyfox.com"]

twitchClientId    = "kuzdmn0w740xkkyuteg5yt6o0fybrq"
twitchCallbackUrl = "https://test.post.postyfox.com/api/Twitch_SubscriptionCallBack"

# Container App Configuration
container_registry_url              = "ghcr.io"
container_registry_username         = "postyfox"
# Note: container_registry_password is stored in Key Vault as "ContainerRegistryPassword"
container_image_name                = "postyfox/postyfox-frontend"
container_image_tag                 = "test-latest"
container_app_custom_domain_enabled = false
container_app_logs                  = ["ContainerAppConsoleLogs", "ContainerAppSystemLogs"]