# postyfox-core Contributors Guide

## Getting started

### Requirements

Visual Studio Code, Visual Studio 2026 or JetBrains Rider (Note, JetBrains Rider has a free, non-commercial license)
Docker or Podman (for local containerised development)

### Testing against the API with Postman

Postman is one of the easiest ways to experiment with the API's that are deployed. But in order to do this you need to 
authenticate and get a Bearer Token or you will receive 401's.

### Standing up a local dev stack

There is a docker-compose file in the deploy directory that will stand up a local dev stack. This is the easiest way 
to get started with development and testing.
Note the comment at the top of the file; 

```
# PostyFox local stack. Quick start:  docker compose up --build
# Adds the OIDC edge (Keycloak + oauth2-proxy):  docker compose --profile auth up --build
#
# By default the APIs run with Auth__DevMode=true (no login needed) for fast local iteration.
# The 'auth' profile brings the production-representative oauth2-proxy -> Keycloak path.
```

You will want to do a docker compose up --build to get the stack running. 
If you want to test the auth path, you will need to run docker compose --profile auth up --build.

The auth path includes a full, complete, Keycloak and all parts needed to validate locally without having any
external dependencies. Credentials which are imported and available for use can be found in the deploy/keycloak 
directory. The default username is `postyfox` and the password is `postyfox`.

## REMINDER

DO NOT checkin any passwords, secrets or other values if you use the local.settings.json file. *PLEASE* use local 
environment variables for these values for development work to make it less likely that you do so.  We do have scanning 
setup to try and detect this, but it is not fool proof. 

The SecretStore variable is a reference to KeyVault - and if left blank should default to a code path in all cases where 
everything runs local, but you may need to check specific code paths.

## Submitting Issues

If you have found a bug, something that isn't right or would like to request a feature, please log it on our main 
project tracker - we will then triage it to the right project for resolution. A single issue might actually spawn multiple tickets for different people to work on to be fully closed.

