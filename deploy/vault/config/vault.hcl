# HashiCorp Vault server config for the PostyFox dev/prod stacks.
#
# Uses the integrated file storage backend (persisted to the `vaultdata` volume) with the default
# Shamir seal. The stack is initialised + unsealed automatically by the `vault-init` sidecar, which
# writes the generated unseal keys + root token to the `vaultkeys` volume and re-applies them on
# every start (see deploy/vault/bootstrap.sh).

ui = true

storage "file" {
  path = "/vault/file"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  # TLS is terminated at the edge / handled on the internal network only — Vault never publishes a
  # host port (same rule as the APIs). Front it with a TLS terminator if you expose it.
  tls_disable = true
}

# Address other cluster members / the CLI use to reach this node (compose service DNS name).
api_addr = "http://vault:8200"

# Containers frequently can't lock memory (rootless podman, missing IPC_LOCK); disabling mlock keeps
# Vault from refusing to start. Acceptable with the file backend here; enable it + grant IPC_LOCK if
# your host supports it.
disable_mlock = true
