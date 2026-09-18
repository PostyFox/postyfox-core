import { test } from "node:test";
import assert from "node:assert/strict";
import { cookiePairingDiagnostics } from "../src/diagnostics.js";

test("cookiePairingDiagnostics reports cookie names and the paired UA", () => {
  const result = cookiePairingDiagnostics(
    JSON.stringify({
      CookieHeader: "a=session-a; b=session-b; cf_clearance=cleared-token",
      UserAgent: "Mozilla/5.0 (TestBrowser)",
    }),
  );

  assert.deepEqual(result, {
    userAgent: "Mozilla/5.0 (TestBrowser)",
    cookieNames: ["a", "b", "cf_clearance"],
  });
});

test("cookiePairingDiagnostics never surfaces cookie values", () => {
  const result = cookiePairingDiagnostics(
    JSON.stringify({ CookieHeader: "laravel_session=super-secret-value" }),
  );
  const serialized = JSON.stringify(result);
  assert.doesNotMatch(serialized, /super-secret-value/);
  assert.deepEqual(result, { userAgent: null, cookieNames: ["laravel_session"] });
});

test("cookiePairingDiagnostics reports a null userAgent when none was paired", () => {
  const result = cookiePairingDiagnostics(JSON.stringify({ CookieHeader: "a=one" }));
  assert.equal(result?.userAgent, null);
});

test("cookiePairingDiagnostics returns null for a non-cookie secret (OAuth-style connectors)", () => {
  assert.equal(cookiePairingDiagnostics(JSON.stringify({ AccessToken: "tok" })), null);
});

test("cookiePairingDiagnostics returns null for a missing secret", () => {
  assert.equal(cookiePairingDiagnostics(null), null);
  assert.equal(cookiePairingDiagnostics(undefined), null);
  assert.equal(cookiePairingDiagnostics(""), null);
});

test("cookiePairingDiagnostics returns null for malformed JSON rather than throwing", () => {
  assert.equal(cookiePairingDiagnostics("{not json"), null);
});

test("cookiePairingDiagnostics returns null for a non-object secret", () => {
  assert.equal(cookiePairingDiagnostics(JSON.stringify("just a string")), null);
  assert.equal(cookiePairingDiagnostics(JSON.stringify(42)), null);
});
