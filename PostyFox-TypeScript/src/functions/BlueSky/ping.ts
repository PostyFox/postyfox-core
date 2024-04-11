import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"

async function BlueSky_Ping(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    return { body: request.headers.values.toString() };
}

app.http('BlueSky_Ping', {
    methods: ['GET'],
    handler: BlueSky_Ping
});