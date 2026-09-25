import { test } from "node:test";
import assert from "node:assert/strict";
import { describeError } from "../src/connectors/errors.js";

test("describeError surfaces an axios response body (the megalodon 400 case)", () => {
  // Shape thrown by axios (megalodon's client) on a non-2xx: message hides the detail, response.data has it.
  const axiosErr = Object.assign(new Error("Request failed with status code 400"), {
    response: {
      status: 400,
      data: { error: { message: "Text, attaches or poll is required.", code: "CONTENT_REQUIRED" } },
    },
  });
  const detail = describeError(axiosErr);
  assert.match(detail, /^HTTP 400:/);
  assert.match(detail, /CONTENT_REQUIRED/);
  assert.match(detail, /Text, attaches or poll is required/);
});

test("describeError handles an axios response with a string body", () => {
  const axiosErr = Object.assign(new Error("Request failed with status code 413"), {
    response: { status: 413, data: "Payload Too Large" },
  });
  assert.equal(describeError(axiosErr), "HTTP 413: Payload Too Large");
});

test("describeError includes status + code for an atproto XRPCError", () => {
  // XRPCError carries a numeric status and a machine-readable error code alongside the message.
  const xrpcErr = Object.assign(new Error("Invalid app password"), {
    status: 401,
    error: "AuthRequired",
  });
  assert.equal(describeError(xrpcErr), "HTTP 401 AuthRequired: Invalid app password");
});

test("describeError includes X's error entries for a rettiwt TwitterError", () => {
  const twitterErr = Object.assign(new Error("Request failed with status code 403"), {
    status: 403,
    details: [{ code: 353, message: "This request requires a matching csrf cookie and header.", name: "", type: undefined }],
  });
  const detail = describeError(twitterErr);
  assert.match(detail, /^HTTP 403: Request failed with status code 403: /);
  assert.match(detail, /matching csrf cookie/);
});

test("describeError omits empty rettiwt TwitterError details", () => {
  const twitterErr = Object.assign(new Error("Request failed with status code 403"), {
    status: 403,
    details: [{ code: undefined, message: undefined, name: undefined, type: undefined }],
  });
  assert.equal(describeError(twitterErr), "HTTP 403: Request failed with status code 403");
});

test("describeError follows the cause of a wrapped error", () => {
  const inner = new Error("Unable to resolve the X ondemand chunk URL from the homepage runtime.");
  const wrapped = new Error("Unknown error", { cause: inner });
  assert.equal(
    describeError(wrapped),
    "Unknown error (cause: Unable to resolve the X ondemand chunk URL from the homepage runtime.)",
  );
});

test("describeError attaches a parsed body when present (tumblr-style)", () => {
  const tumblrErr = Object.assign(new Error("API error: 400 Bad Request"), {
    body: { errors: [{ title: "Bad Request", detail: "tags too long", code: 1016 }] },
  });
  const detail = describeError(tumblrErr);
  assert.match(detail, /API error: 400 Bad Request/);
  assert.match(detail, /tags too long/);
});

test("describeError truncates an oversized body", () => {
  const huge = "x".repeat(5000);
  const detail = describeError(Object.assign(new Error("boom"), { response: { status: 400, data: huge } }));
  assert.ok(detail.length < huge.length);
  assert.match(detail, /\(5000 chars\)$/);
});

test("describeError falls back to the message for a plain Error", () => {
  assert.equal(describeError(new Error("something broke")), "something broke");
});

test("describeError stringifies a non-Error value", () => {
  assert.equal(describeError("weird"), "weird");
});
