import { initializeApp } from "firebase-admin/app";
import { getFirestore } from "firebase-admin/firestore";
import { defineSecret, defineString } from "firebase-functions/params";
import { onRequest, Request } from "firebase-functions/v2/https";
import { onSchedule } from "firebase-functions/v2/scheduler";
import * as logger from "firebase-functions/logger";
import { getProductPurchase, listVoided, playerIdFromAuthCode } from "./google";
import { processVoided, registerPurchase, RuleError, syncDeductions } from "./logic";
import { FirestoreStore } from "./store";

initializeApp();
const db = getFirestore();
const store = new FirestoreStore(db);

// Set with `firebase functions:config` replacements (params): see Server/README.md.
const PACKAGE_NAME = defineString("PLAY_PACKAGE_NAME", { default: "com.glorpfarm.app" });
const PGS_WEB_CLIENT_ID = defineString("PGS_WEB_CLIENT_ID");
const PGS_WEB_CLIENT_SECRET = defineSecret("PGS_WEB_CLIENT_SECRET");

const REGION = "southamerica-east1";
const DAY = 24 * 60 * 60 * 1000;

function body(req: Request): Record<string, unknown> {
  return typeof req.body === "object" && req.body !== null ? (req.body as Record<string, unknown>) : {};
}

function text(value: unknown, max = 4096): string {
  return typeof value === "string" && value.length <= max ? value : "";
}

async function handle(req: Request, res: { status(code: number): { json(value: unknown): void } }, run: () => Promise<unknown>) {
  if (req.method !== "POST") {
    res.status(405).json({ error: "POST only" });
    return;
  }
  try {
    res.status(200).json(await run());
  } catch (error) {
    if (error instanceof RuleError) {
      res.status(error.status).json({ error: error.message });
      return;
    }
    logger.error("refunds: unexpected error", error);
    res.status(500).json({ error: "server error" });
  }
}

/**
 * POST { authCode, productId, purchaseToken } — the game delivered a purchase. Checks it with Google Play
 * and remembers which player owns it, so a later refund can take its gems back.
 */
export const registerpurchase = onRequest(
  { region: REGION, secrets: [PGS_WEB_CLIENT_SECRET], maxInstances: 5 },
  (req, res) =>
    handle(req, res, async () => {
      const b = body(req);
      const productId = text(b.productId, 100);
      const purchaseToken = text(b.purchaseToken);
      const playerId = await playerIdFromAuthCode(text(b.authCode), PGS_WEB_CLIENT_ID.value(), PGS_WEB_CLIENT_SECRET.value());
      const play = await getProductPurchase(PACKAGE_NAME.value(), productId, purchaseToken);
      const record = await registerPurchase(store, playerId, productId, purchaseToken, play, Date.now());
      return { ok: true, voided: record.voided };
    }),
);

/**
 * POST { authCode, applied: [ids] } — the game confirms the deductions it already applied and receives the
 * ones still waiting: { pending: [{ id, gems, productId }] }.
 */
export const refundsync = onRequest(
  { region: REGION, secrets: [PGS_WEB_CLIENT_SECRET], maxInstances: 5 },
  (req, res) =>
    handle(req, res, async () => {
      const b = body(req);
      const applied = Array.isArray(b.applied) ? b.applied.map((v) => text(v, 64)).filter((v) => v.length === 64) : [];
      const playerId = await playerIdFromAuthCode(text(b.authCode), PGS_WEB_CLIENT_ID.value(), PGS_WEB_CLIENT_SECRET.value());
      return { pending: await syncDeductions(store, playerId, applied) };
    }),
);

/** Every 6 hours: reads Google Play's voided purchases and creates the deductions. */
export const checkvoided = onSchedule({ region: REGION, schedule: "every 6 hours" }, async () => {
  const metaRef = db.collection("meta").doc("voided");
  const meta = await metaRef.get();
  const now = Date.now();
  const last = Number(meta.get("lastCheckMillis") ?? 0);
  // One hour of overlap between runs; Google only keeps the last 30 days.
  const start = Math.max(now - 30 * DAY + 60_000, last - 60 * 60 * 1000);
  const voided = await listVoided(PACKAGE_NAME.value(), start);
  const created = await processVoided(store, voided, now);
  await metaRef.set({ lastCheckMillis: now, lastFound: voided.length, lastCreated: created });
  logger.info(`refunds: ${voided.length} voided purchases read, ${created} new deductions`);
});
