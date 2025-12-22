environment="test"

# OIDC Authentication Configuration
# Replace these values with your OIDC provider details (Keycloak, Auth0, Okta, etc.)
oidc_client_id="pfapis"
oidc_issuer="https://auth.postyfox.com/realms/PostyFox"
openid_configuration_endpoint="https://auth.postyfox.com/realms/PostyFox/.well-known/openid-configuration"
logout_endpoint = "/.auth/logout"

kv_logs = ["AuditEvent"]

cors = ["https://test.cp.postyfox.com"]

twitchClientId    = "kuzdmn0w740xkkyuteg5yt6o0fybrq"
twitchCallbackUrl = "https://test.post.postyfox.com/api/Twitch_SubscriptionCallBack"
