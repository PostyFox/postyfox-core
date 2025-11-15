environment="dev"

# OIDC Authentication Configuration
# Replace these values with your OIDC provider details (Keycloak, Auth0, Okta, etc.)
oidc_client_id="9b930c86-ea5b-40d0-a200-000000000010"
oidc_issuer="https://auth.postyfox.com/realms/PostyFox"
openid_configuration_endpoint="https://auth.postyfox.com/realms/PostyFox/.well-known/openid-configuration"

kv_logs = ["AuditEvent"]

cors = ["https://dev.cp.postyfox.com", "http://localhost:4200", "https://portal.azure.com"]

twitchClientId    = "kuzdmn0w740xkkyuteg5yt6o0fybrq"
twitchCallbackUrl = "https://dev.post.postyfox.com/api/Twitch_SubscriptionCallBack"