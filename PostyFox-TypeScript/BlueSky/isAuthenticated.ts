import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
import { BskyAgent } from "@atproto/api";
import { SecretClient } from "@azure/keyvault-secrets";
import { TableClient } from "@azure/data-tables";
import { DefaultAzureCredential } from "@azure/identity";
const { getUserId, validateAuth } = require("../Helpers/index");

async function BlueSky_IsAuthenticated(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    if (validateAuth(request)) {
        const credential = new DefaultAzureCredential();
        let body = (await request.body.getReader().read()).value;

        // Extract the serviceId from the POST JSON body
        if (body) {
            let jsonObject = JSON.parse(body);
            
            if (jsonObject.ServiceID) {
                // Service config for the user is held in ConfigTable
                const configTableName = process.env["ConfigTable"];
                if(!configTableName) throw new Error("ConfigTable is empty");

                const client = new TableClient(configTableName, "ConfigTable", credential);
                await client.createTable(); // Ensure it exists

                const entity = await client.getEntity(getUserId(request), jsonObject.ServiceID);
                if (entity && entity.ServiceID == "BlueSky") {
                    let configurationJson = JSON.parse(entity.Configuration.toString());
                    // The BlueSky identity for a user is made up of a SecureConfiguration component which is stored in KeyVault (their App Password)
                    // So we need to pull it now
                    const keyVaultName = process.env["SecretStore"];
                    if(!keyVaultName) throw new Error("SecretStore is empty");

                    const secretClient = new SecretClient(keyVaultName, credential);
                    let secretName = jsonObject.ServiceID + "-" + getUserId(request);
                    let secret = await secretClient.getSecret(secretName);

                    // Extract the password from the secret data, and try logging into the BSky platform. Return result.
                    let secretJson = JSON.parse(secret.value);
                    if (secretJson) {
                        const agent = new BskyAgent({ service: 'https://bsky.social' });
                        await agent.login({
                            identifier: configurationJson.Handle,
                            password: secretJson.AppPassword,
                        }).then(res => { return res.success })
                    }
                }
            }
        }
        return { status: 404 }
    }

    return { body: "false" };
}

app.http('BlueSky_IsAuthenticated', {
    methods: ['POST'],
    handler: BlueSky_IsAuthenticated
});