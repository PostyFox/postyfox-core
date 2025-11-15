# postyfox-core Contributors Guide

## Getting started

### Requirements

[Azure Function App Runtime v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp#install-the-azure-functions-core-tools)

[Azurite Storage Emulator](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)

Visual Studio Code or Visual Studio 2022

### Setting up Azurite

By far the easiest way of using / configuring Azurite is to use the [Visual Studio Code extension](https://marketplace.visualstudio.com/items?itemName=Azurite.azurite). This will add a number of options along the bottom of your VS Code instance - such as Azurite Table Service, Azurite Queue Service etc, and simply clicking on these will start the relevant service. 

The local.settings.json file for each project should *already* be configured correctly for working off the Azurite storage emulator and not require any changes, unless you are using different ports, however please do check the configuration.

If you encounter issues with TLS / Token auth, you may need to tweak your Azurite config to use TLS - to do this a [good blog post is here](https://blog.jongallant.com/2020/04/local-azure-storage-development-with-azurite-azuresdks-storage-explorer/) - no point rewriting it all!  Configuration can then be completed under the Azurite Extension settings in Visual Studio Code, and after restarting the emulator you will find your session is using https.

You might also want to use [Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer/) to browse, and configure values directly on your local storage layer. 

### Using the Function App Runtime

You can spin up a Function App host locally, once the FA functions are compiled (dotnet build or yarn etc); go into the output folder and then simply run func start to kick the host into life.  This gives you a way to debug things locally, and it should work from within VS Core and VS.  Note that VS Core needs the Function App Extension installed (strongly recommended).

### Testing against the API with Postman

Postman is one of the easiest ways to experiment with the API's that are deployed. But in order to do this you need to authenticate and get a Bearer Token or you will receive 401's.

The process to receive a token depends on your OIDC provider configuration. 

**For Development/Testing:**
1. Navigate to your OIDC provider's authorization endpoint
2. Authenticate with your credentials
3. You will receive a JWT token which you can use for API requests

**Example OIDC Authorization Flow:**
```
GET https://your-oidc-provider.example.com/auth?
  client_id=YOUR_CLIENT_ID&
  redirect_uri=https://jwt.ms&
  scope=openid profile email&
  response_type=id_token&
  nonce=defaultNonce
```

Open up Postman, and start a request to an API - for example - https://postyfox-func-app-dotnet-dev.azurewebsites.net/api/Services? - and formulate it with the correct parameters. Switch to the Authentication tab, select Bearer Token and paste your token into the box. Click Send. And that is it! If you have provided all the right parameters, and everything is working, you will get a response.

If you encounter an error, you should get an error back, or you may need to do some debugging.

If you are running the Function App stack locally, set two environment variables locally on your machine - or alternatively set them in the projects local.settings.json file.

|Environment Variable|Value|
|---|---|
|PostyFoxDevMode|true|
|PostyFoxUserID|*Username*|

Replace username with a username for display and processing in the UI.

### Testing the API / Checking the Swagger Docs

Point your browser at a deployed instance, such as the [dev one](https://postyfox-func-app-dotnet-dev.azurewebsites.net/api/swagger/ui). 

**Note:** If authentication is enabled, you will need to authenticate through your OIDC provider before accessing the Swagger UI. The authentication flow will redirect you to login at:
```
https://postyfox-func-app-dotnet-dev.azurewebsites.net/.auth/login/OpenIDAuth/callback
```

You will be able to send test requests to the API and review the responses. Note that this functionality is not enabled on all deployments.

## REMINDER

DO NOT checkin any passwords, secrets or other values if you use the local.settings.json file. *PLEASE* use local environment variables for these values for development work to make it less likely that you do so.  We do have scanning setup to try and detect this, but it is not fool proof. 

The SecretStore variable is a reference to KeyVault - and if left blank should default to a code path in all cases where everything runs local, but you may need to check specific code paths.

## Submitting Issues

If you have found a bug, something that isn't right or would like to request a feature, please log it on our main project tracker - we will then triage it to the right project for resolution. A single issue might actually spawn multiple tickets for different people to work on to be fully closed.

