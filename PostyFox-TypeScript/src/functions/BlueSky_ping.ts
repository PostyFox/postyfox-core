import { app, HttpResponseInit, HttpRequest, InvocationContext } from "@azure/functions"
const { flattenHeaders } = require("../Helpers/index");

async function BlueSky_Ping(request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> {
    return { body: flattenHeaders(request.headers) };
}

app.http('BlueSky_Ping', {
    methods: ['GET'],
    authLevel: "anonymous",
    handler: BlueSky_Ping
});