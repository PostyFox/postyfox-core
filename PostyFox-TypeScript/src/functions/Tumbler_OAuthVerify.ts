// This callback will be used to verify the code etc as part of the 3 leg auth from Tumblr

import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
import { SecretClient } from "@azure/keyvault-secrets";
import { DefaultAzureCredential } from "@azure/identity";
const { getUserId, validateAuth, generateRandomString } = require("../Helpers/index");

async function Tumblr_OAuthVerify(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    // Extract code and state query string parameters from the response
    // Check that state was one we sent with the original request
    if (request.query.has("code") && request.query.has("state")) {
        const keyVaultName = process.env["SecretStore"];
        if(!keyVaultName) throw new Error("SecretStore is empty");
        const credential = new DefaultAzureCredential();
        const secretClient = new SecretClient(keyVaultName, credential);
        let userId = getUserId(request);
        let secret = await secretClient.getSecret("TumblrState-" + userId); // Get the state from store
        let secretClientId = await secretClient.getSecret("TumblrClientId");
        let secretClientSecret = await secretClient.getSecret("TumblrClientSecret");
        const tumblrRedirectUri = process.env["TumblrAuthRedirect"];

        if (secret != null && secret.value != null)
        {
            if (secret.value == request.query.get("state")) {
                // State matches - woo!
                // Continue with auth flow.
                let accessTokenUri = "https://api.tumblr.com/v2/oauth2/token"; // POST op to this
                
                fetch(accessTokenUri, {
                    method: 'POST',
                    body: "grant_type=authorization_code&code=" + request.query.get("code") + "&client_id=" + secretClientId.value + "&client_secret=" + secretClientSecret.value + "&redirect_uri=" + tumblrRedirectUri,
                    headers: {
                        "Content-Type": "application/x-www-form-urlencoded",
                    },
                })
                .then(response => response.json())
                .then(async data => {
                    // Remove the state from keyvault
                    await secretClient.beginDeleteSecret("TumblrState-" + userId);
                    // Save the access token - set the expiry time too
                    let expiryTime = new Date(new Date().getTime() + data.expires_in * 1000);
                    await secretClient.setSecret("TumblrAccessToken-" + userId, data.access_token, { expiresOn: expiryTime, tags: { ["Service"]: "Tumblr" }} );
                    // Save the refresh token
                    await secretClient.setSecret("TumblrRefreshToken-" + userId, data.refresh_token, { expiresOn: expiryTime, tags: { ["Service"]: "Tumblr" }} );

                    return {
                        status: 200
                    }
                });
            }
        }

        return {
            status: 404
        }
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

        let state = generateRandomString(20);
        let userId = getUserId(request);

        await secretClient.setSecret("TumblrState-" + userId, state); // Save the state so we can validate it later

        let authUrl = "www.tumblr.com/oauth2/authorize?";
        authUrl += "client_id=" + secret.value + "&response_type=code&scope=basic%20write&state=" + state + "redirect_uri=" + tumblrRedirectUri

        return {
            status: 200,
            body: "{\"AuthURL\":\"" + authUrl + "\"}"
        }
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