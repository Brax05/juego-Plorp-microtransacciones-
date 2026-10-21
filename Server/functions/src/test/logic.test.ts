import { test } from "node:test";
import assert from "node:assert/strict";
import type { Deduction, OrphanVoid, PurchaseRecord, Store } from "../logic";
import { processVoided, registerPurchase, syncDeductions, tokenHash, RuleError } from "../logic";

class MemoryStore implements Store {
  purchases = new Map<string, PurchaseRecord>();
  deductions = new Map<string, Deduction>();
  orphans = new Map<string, OrphanVoid>();
  async getPurchase(id: string) { return this.purchases.get(id) && { ...this.purchases.get(id)! }; }
  async putPurchase(id: string, v: PurchaseRecord) { this.purchases.set(id, { ...v }); }
  async getDeduction(id: string) { return this.deductions.get(id) && { ...this.deductions.get(id)! }; }
  async putDeduction(v: Deduction) { this.deductions.set(v.id, { ...v }); }
  async pendingDeductions(p: string) { return [...this.deductions.values()].filter((d) => d.playerId === p && !d.applied); }
  async getOrphan(id: string) { return this.orphans.get(id); }
  async putOrphan(id: string, v: OrphanVoid) { this.orphans.set(id, v); }
  async deleteOrphan(id: string) { this.orphans.delete(id); }
}

const ok = { purchaseState: 0, orderId: "GPA.1", purchaseTimeMillis: 1 };

test("a refunded purchase takes its gems back once", async () => {
  const s = new MemoryStore();
  await registerPurchase(s, "p1", "plorp.gems.120", "tok-a", ok, 10);
  assert.equal(await processVoided(s, [{ purchaseToken: "tok-a", orderId: "GPA.1", voidedTimeMillis: 20, voidedReason: 1 }], 30), 1);
  assert.equal(await processVoided(s, [{ purchaseToken: "tok-a", orderId: "GPA.1", voidedTimeMillis: 20, voidedReason: 1 }], 40), 0, "voided list overlap");
  const pending = await syncDeductions(s, "p1", []);
  assert.deepEqual(pending, [{ id: tokenHash("tok-a"), gems: 120, productId: "plorp.gems.120" }]);
  assert.deepEqual(await syncDeductions(s, "p1", [pending[0].id]), [], "confirmed deductions stop coming");
});

test("another player can't see or confirm my deductions", async () => {
  const s = new MemoryStore();
  await registerPurchase(s, "p1", "plorp.gems.20", "tok-b", ok, 10);
  await processVoided(s, [{ purchaseToken: "tok-b", orderId: "x", voidedTimeMillis: 1, voidedReason: 7 }], 2);
  assert.deepEqual(await syncDeductions(s, "p2", [tokenHash("tok-b")]), []);
  assert.equal((await syncDeductions(s, "p1", [])).length, 1);
});

test("a token registered by another player is refused", async () => {
  const s = new MemoryStore();
  await registerPurchase(s, "p1", "plorp.gems.45", "tok-c", ok, 10);
  await assert.rejects(registerPurchase(s, "p2", "plorp.gems.45", "tok-c", ok, 11), (e: unknown) => e instanceof RuleError && e.status === 409);
  await registerPurchase(s, "p1", "plorp.gems.45", "tok-c", ok, 12); // retry by the owner is fine
});

test("voided before registering: registering creates the deduction", async () => {
  const s = new MemoryStore();
  await processVoided(s, [{ purchaseToken: "tok-d", orderId: "o", voidedTimeMillis: 1, voidedReason: 0 }], 2);
  const record = await registerPurchase(s, "p1", "plorp.gems.560", "tok-d", ok, 3);
  assert.equal(record.voided, true);
  assert.equal((await syncDeductions(s, "p1", []))[0].gems, 560);
  assert.equal(s.orphans.size, 0);
});

test("canceled or unknown purchases", async () => {
  const s = new MemoryStore();
  const r = await registerPurchase(s, "p1", "plorp.starter.150", "tok-e", { ...ok, purchaseState: 1 }, 3);
  assert.equal(r.voided, true);
  assert.equal((await syncDeductions(s, "p1", []))[0].gems, 150);
  await assert.rejects(registerPurchase(s, "p1", "plorp.gems.999", "tok-f", ok, 3), (e: unknown) => e instanceof RuleError && e.status === 400);
  await assert.rejects(registerPurchase(s, "p1", "plorp.gems.20", "tok-g", { ...ok, purchaseState: 2 }, 3), (e: unknown) => e instanceof RuleError && e.status === 409);
});
