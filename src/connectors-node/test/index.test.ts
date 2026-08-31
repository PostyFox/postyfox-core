import { test } from "node:test";
import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";

const entry = path.resolve(fileURLToPath(import.meta.url), "../../src/index.ts");

test("refuses to start when INTERNAL_TOKEN is not set", () => {
  const env = { ...process.env };
  delete env.INTERNAL_TOKEN;

  const result = spawnSync("node", ["--import", "tsx", entry], { env, encoding: "utf8", timeout: 5000 });

  assert.equal(result.status, 1);
});
