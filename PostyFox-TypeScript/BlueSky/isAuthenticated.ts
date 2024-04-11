import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
import { BskyAgent } from "@atproto/api";

async function BlueSky_IsAuthenticated(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    return { body: "" };
}

app.http('BlueSky_IsAuthenticated', {
    methods: ['GET'],
    handler: BlueSky_IsAuthenticated
});