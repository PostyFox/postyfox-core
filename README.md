# postyfox-core

## Repository Purpose

This repository contains the Azure Function Apps that provide core services, and the IaC Terraform code for deployment activities.

Github actions exist here which manage full deployment pipelines for all components crucial to the platform.

## Getting started

### Requirements

[Azure Function App Runtime v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Cisolated-process%2Cnode-v4%2Cpython-v2%2Chttp-trigger%2Ccontainer-apps&pivots=programming-language-csharp#install-the-azure-functions-core-tools)

[Azurite Storage Emulator](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite)

Visual Studio Code or Visual Studio 2022

### Using the Function App Runtime

You can spin up a Function App host locally, once the FA functions are compiled (dotnet build or yarn etc); go into the output folder and then simply run func start to kick the host into life.  This gives you a way to debug things locally, and it should work from within VS Core and VS.  Note that VS Core needs the Function App Extension installed (strongly recommended).

### Specific requirements for projects

#### C# Dot Net Core Function App

You will need to set the Environment Variable ConfigTable to the Connection String endpoint of the Azurite Emulator; this table is used for all configuration by the .NET based Function App at present.

#### NodeJS Core Function App

TBD

### Testing against the API with Postman

Postman is one of the easiest ways to experiment with the API's that are deployed. But in order to do this you need to authenticate and get a Bearer Token or you will receive 401's.

The process to receive a token is fairly straight forward. (Click here)[https://postyfoxdev.b2clogin.com/postyfoxdev.onmicrosoft.com/oauth2/v2.0/authorize?p=B2C_1_Signin&client_id=2b89259d-3cc3-41fe-adbf-5f9acb15e622&nonce=defaultNonce&redirect_uri=https%3A%2F%2Fjwt.ms&scope=openid&response_type=id_token&prompt=login], and login.  You will be presented with a token in the test JWT.MS application, copy this into your clipboard.

Open up Postman, and start a request to an API - for example - https://postyfox-func-app-dotnet-dev.azurewebsites.net/api/Services? - and formulate it with the correct parameters. Switch to the Authentication tab, select Bearer Token and paste your token into the box. Click Send. And that is it! If you have provided all the right parameters, and everything is working, you will get a response.

If you encounter an error, you should get an error back, or you may need to do some debugging.

If you are running the Function App stack locally, set two environment variables locally on your machine.

|Environment Variable|Value|
|---|---|
|PostyFoxDevMode|true|
|PostyFoxUserID|*Username*|

Replace username with a username for display and processing in the UI.