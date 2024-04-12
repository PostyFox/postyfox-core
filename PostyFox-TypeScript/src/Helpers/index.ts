import { HttpRequest } from "@azure/functions"

const AUTH_HEADER = "X-MS-CLIENT-PRINCIPAL";
const AUTH_HEADER_ID = "X-MS-CLIENT-PRINCIPAL-ID";

// Identifies if the user has an active session or not
exports.validateAuth = function(request: HttpRequest): boolean {
    const isDevMode = process.env["PostyFoxDevMode"];
    if (!isDevMode) {
        return request.headers.has(AUTH_HEADER);
    } else {
        return true;
    }
};

// Gets the authenticated UserID
exports.getUserId = function(request: HttpRequest): string {
    const isDevMode = process.env["PostyFoxDevMode"];
    if (!isDevMode) {
        return request.headers.get(AUTH_HEADER_ID);
    } else {
        return process.env["PostyFoxUserID"];
    }
};

exports.flattenHeaders = function(headers: Headers): string {
    let response = "";
    headers.forEach((v,k) => {
        response += k + "=" + v + "\n";
    });

    return response;
}