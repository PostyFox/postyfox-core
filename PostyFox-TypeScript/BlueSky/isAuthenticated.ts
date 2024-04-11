import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
import { BskyAgent } from "@atproto/api";
import { SecretClient } from "@azure/keyvault-secrets";
import { TableClient } from "@azure/data-tables";
import { DefaultAzureCredential } from "@azure/identity";
const { getUserId, validateAuth } = require("../Helpers/index");

async function BlueSky_IsAuthenticated(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    if (validateAuth(request)) {
        const credential = new DefaultAzureCredential();

        // Service config for the user is held in ConfigTable
        const configTableName = process.env["ConfigTable"];
        if(!configTableName) throw new Error("ConfigTable is empty");

        const client = new TableClient(configTableName, "ConfigTable", credential);
        await client.createTable(); // Ensure it exists

        const entity = await client.getEntity(getUserId(request), "BlueSky");

        // The BlueSky identity for a user is made up of a SecureConfiguration component which is stored in KeyVault (their App Password)
        // So we need to pull it now

        

        const keyVaultName = process.env["SecretStore"];
        if(!keyVaultName) throw new Error("SecretStore is empty");

    }

    return { body: "" };


}

app.http('BlueSky_IsAuthenticated', {
    methods: ['GET'],
    handler: BlueSky_IsAuthenticated
});