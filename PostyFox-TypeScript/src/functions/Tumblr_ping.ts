import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"

async function Tumblr_Ping(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    return { body: request.headers.values.toString() };
}

app.http('Tumblr_Ping', {
    methods: ['GET'],
    authLevel: "anonymous",
    handler: Tumblr_Ping
});