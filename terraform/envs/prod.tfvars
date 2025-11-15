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