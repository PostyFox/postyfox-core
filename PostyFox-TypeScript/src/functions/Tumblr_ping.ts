import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
const { flattenHeaders } = require("../Helpers/index");

async function Tumblr_Ping(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    return { body: flattenHeaders(request.headers) };
}

app.http('Tumblr_Ping', {
    methods: ['GET'],
    authLevel: "anonymous",
    handler: Tumblr_Ping
});