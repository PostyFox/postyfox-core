// This callback will be used to verify the code etc as part of the 3 leg auth from Tumblr

import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
import { SecretClient } from "@azure/keyvault-secrets";
import { DefaultAzureCredential } from "@azure/identity";
const { getUserId, validateAuth } = require("../Helpers/index");

async function Tumblr_OAuthVerify(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    // Extract code and state query string parameters from the response
    // Check that state was one we sent with the original request
    if (request.query.has("code") && request.query.has("state")) {

    }


    return {
        status: 403
     };
}

async function Tumbler_GenerateAuthUrl(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    if (validateAuth(request)) {
        const tumblrRedirectUri = process.env["TumblrAuthRedirect"];

        const keyVaultName = process.env["SecretStore"];
        if(!keyVaultName) throw new Error("SecretStore is empty");
        const credential = new DefaultAzureCredential();
        const secretClient = new SecretClient(keyVaultName, credential);
        let secret = await secretClient.getSecret("TumblrClientId");

        // Generate an auth url, save the state code and pass back to user
        let authUrl = "www.tumblr.com/oauth2/authorize?";
        authUrl += "client_id=" + secret.value + "&response_type=code&scope=basic%20write&state=<>redirect_uri=" + tumblrRedirectUri
    }

    return {
        status: 403
     };
}

app.http('Tumblr_OAuthVerify', {
    methods: ['GET'],
    authLevel: "anonymous",
    handler: Tumblr_OAuthVerify
});

app.http('Tumbler_GenerateAuthUrl', {
    methods: ['GET'],
    authLevel: "anonymous",
    handler: Tumbler_GenerateAuthUrl
});