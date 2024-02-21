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