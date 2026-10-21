import type { Firestore } from "firebase-admin/firestore";
import type { Deduction, OrphanVoid, PurchaseRecord, Store } from "./logic";

/**
 * Firestore collections:
 * - purchases/{tokenHash}   who owns each delivered purchase
 * - deductions/{tokenHash}  gems to take back from a player (applied = the game already did)
 * - orphanVoids/{tokenHash} voided purchases seen before the game registered them
 * - meta/voided             when the voided list was last read
 */
export class FirestoreStore implements Store {
  constructor(private readonly db: Firestore) {}

  async getPurchase(id: string) {
    const doc = await this.db.collection("purchases").doc(id).get();
    return doc.exists ? (doc.data() as PurchaseRecord) : undefined;
  }

  async putPurchase(id: string, value: PurchaseRecord) {
    await this.db.collection("purchases").doc(id).set(value);
  }

  async getDeduction(id: string) {
    const doc = await this.db.collection("deductions").doc(id).get();
    return doc.exists ? (doc.data() as Deduction) : undefined;
  }

  async putDeduction(value: Deduction) {
    await this.db.collection("deductions").doc(value.id).set(value);
  }

  async pendingDeductions(playerId: string) {
    const snap = await this.db
      .collection("deductions")
      .where("playerId", "==", playerId)
      .where("applied", "==", false)
      .limit(50)
      .get();
    return snap.docs.map((d) => d.data() as Deduction);
  }

  async getOrphan(id: string) {
    const doc = await this.db.collection("orphanVoids").doc(id).get();
    return doc.exists ? (doc.data() as OrphanVoid) : undefined;
  }

  async putOrphan(id: string, value: OrphanVoid) {
    await this.db.collection("orphanVoids").doc(id).set(value);
  }

  async deleteOrphan(id: string) {
    await this.db.collection("orphanVoids").doc(id).delete();
  }
}
