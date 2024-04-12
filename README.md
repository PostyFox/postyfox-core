# postyfox-core

## Repository Purpose

This repository contains the Azure Function Apps that provide core services, and the IaC Terraform code for deployment activities.

Github actions exist here which manage full deployment pipelines for all components crucial to the platform.

If you'd like to get involved in the project, please check our Contributors Guide for details of the environment configuration, testing and such.

## Submitting Issues

If you have found a bug, something that isn't right or would like to request a feature, please log it on our main project tracker - we will then triage it to the right project for resolution. A single issue might actually spawn multiple tickets for different people to work on to be fully closed.

## REMINDER

DO NOT checkin any passwords, secrets or other values if you use the local.settings.json file. *PLEASE* use local environment variables for these values for development work to make it less likely that you do so.  We do have scanning setup to try and detect this, but it is not fool proof. 

The SecretStore variable is a reference to KeyVault - and if left blank should default to a code path in all cases where everything runs local, but you may need to check specific code paths.