import { createHash } from "crypto";
import { gemsFor } from "./catalog";

/**
 * The refund rules, without Firebase or Google calls, so they can be tested on their own.
 *
 * - Every purchase the game delivers is registered: who bought it (Play Games player id) and how many gems.
 * - Google's "voided purchases" list tells which purchases were refunded or charged back.
 * - Each voided purchase becomes one "deduction" for its player; the game takes those gems away the next
 *   time it syncs and confirms it, so a deduction is applied once.
 * - A voided purchase seen before it was registered waits as an orphan; registering it then creates the
 *   deduction (the player can't escape by delaying the registration).
 */

export interface PurchaseRecord {
  playerId: string;
  productId: string;
  gems: number;
  orderId: string;
  purchaseTimeMillis: number;
  registeredAtMillis: number;
  voided: boolean;
}

export interface Deduction {
  id: string; // = token hash: one per purchase
  playerId: string;
  productId: string;
  gems: number;
  orderId: string;
  reason: string;
  createdAtMillis: number;
  applied: boolean;
}

export interface OrphanVoid {
  orderId: string;
  reason: string;
  voidedTimeMillis: number;
}

/** Storage used by the rules (Firestore in production, a Map in the tests). */
export interface Store {
  getPurchase(id: string): Promise<PurchaseRecord | undefined>;
  putPurchase(id: string, value: PurchaseRecord): Promise<void>;
  getDeduction(id: string): Promise<Deduction | undefined>;
  putDeduction(value: Deduction): Promise<void>;
  pendingDeductions(playerId: string): Promise<Deduction[]>;
  getOrphan(id: string): Promise<OrphanVoid | undefined>;
  putOrphan(id: string, value: OrphanVoid): Promise<void>;
  deleteOrphan(id: string): Promise<void>;
}

/** What Google Play says about one purchase (purchases.products.get). */
export interface PlayPurchase {
  purchaseState: number; // 0 purchased, 1 canceled, 2 pending
  orderId: string;
  purchaseTimeMillis: number;
}

/** One entry of Google Play's voided purchases list. */
export interface VoidedPurchase {
  purchaseToken: string;
  orderId: string;
  voidedTimeMillis: number;
  voidedReason: number;
}

export class RuleError extends Error {
  readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export function tokenHash(purchaseToken: string): string {
  return createHash("sha256").update(purchaseToken, "utf8").digest("hex");
}

const REASONS: Record<number, string> = {
  0: "other",
  1: "remorse",
  2: "not_received",
  3: "defective",
  4: "accidental_purchase",
  5: "fraud",
  6: "friendly_fraud",
  7: "chargeback",
};

export function reasonName(code: number): string {
  return REASONS[code] ?? "other";
}

async function createDeduction(store: Store, id: string, purchase: PurchaseRecord, reason: string, now: number) {
  if (await store.getDeduction(id)) return;
  await store.putDeduction({
    id,
    playerId: purchase.playerId,
    productId: purchase.productId,
    gems: purchase.gems,
    orderId: purchase.orderId,
    reason,
    createdAtMillis: now,
    applied: false,
  });
}

/**
 * The game delivered a purchase: remember who owns it. Google's answer must be a real purchase of this
 * product. Returns the record. A token already registered by someone else is refused.
 */
export async function registerPurchase(
  store: Store,
  playerId: string,
  productId: string,
  purchaseToken: string,
  play: PlayPurchase,
  now: number,
): Promise<PurchaseRecord> {
  const gems = gemsFor(productId);
  if (gems === undefined) throw new RuleError(400, "unknown product");
  if (!purchaseToken) throw new RuleError(400, "missing token");
  if (play.purchaseState === 2) throw new RuleError(409, "purchase still pending");

  const id = tokenHash(purchaseToken);
  const existing = await store.getPurchase(id);
  if (existing && existing.playerId !== playerId) throw new RuleError(409, "purchase belongs to another player");

  const record: PurchaseRecord = existing ?? {
    playerId,
    productId,
    gems,
    orderId: play.orderId,
    purchaseTimeMillis: play.purchaseTimeMillis,
    registeredAtMillis: now,
    voided: false,
  };

  // Already canceled by Google, or voided before it reached us: the gems go back right away.
  const orphan = await store.getOrphan(id);
  if (!record.voided && (play.purchaseState === 1 || orphan)) {
    record.voided = true;
    await store.putPurchase(id, record);
    await createDeduction(store, id, record, orphan ? orphan.reason : "canceled", now);
    if (orphan) await store.deleteOrphan(id);
    return record;
  }
  if (!existing) await store.putPurchase(id, record);
  return record;
}

/** One pass over Google's voided purchases. Returns how many new deductions were created. */
export async function processVoided(store: Store, voided: VoidedPurchase[], now: number): Promise<number> {
  let created = 0;
  for (const v of voided) {
    if (!v.purchaseToken) continue;
    const id = tokenHash(v.purchaseToken);
    const purchase = await store.getPurchase(id);
    if (!purchase) {
      if (!(await store.getOrphan(id)))
        await store.putOrphan(id, { orderId: v.orderId, reason: reasonName(v.voidedReason), voidedTimeMillis: v.voidedTimeMillis });
      continue;
    }
    if (purchase.voided) continue;
    purchase.voided = true;
    await store.putPurchase(id, purchase);
    await createDeduction(store, id, purchase, reasonName(v.voidedReason), now);
    created++;
  }
  return created;
}

/**
 * The game syncs: it confirms the deductions it already applied and gets the ones still waiting.
 * Only the player's own deductions are touched.
 */
export async function syncDeductions(
  store: Store,
  playerId: string,
  appliedIds: string[],
): Promise<Array<{ id: string; gems: number; productId: string }>> {
  for (const id of appliedIds.slice(0, 50)) {
    const d = await store.getDeduction(id);
    if (!d || d.playerId !== playerId || d.applied) continue;
    d.applied = true;
    await store.putDeduction(d);
  }
  const pending = await store.pendingDeductions(playerId);
  return pending.map((d) => ({ id: d.id, gems: d.gems, productId: d.productId }));
}
