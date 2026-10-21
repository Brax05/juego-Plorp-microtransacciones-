import { GoogleAuth } from "google-auth-library";
import { PlayPurchase, RuleError, VoidedPurchase } from "./logic";

const PUBLISHER = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications";

// The function's own service account. It must be invited in Play Console (Users and permissions) with
// "View financial data, orders and cancellation survey responses".
const auth = new GoogleAuth({ scopes: ["https://www.googleapis.com/auth/androidpublisher"] });

/**
 * Turns the one-time code the game got from Play Games (RequestServerSideAccess) into the player's id.
 * Proves the request comes from that player: the code is issued by Google to our game only.
 */
export async function playerIdFromAuthCode(code: string, clientId: string, clientSecret: string): Promise<string> {
  if (!code || code.length > 2048) throw new RuleError(401, "missing auth code");
  const body = new URLSearchParams({
    code,
    client_id: clientId,
    client_secret: clientSecret,
    redirect_uri: "",
    grant_type: "authorization_code",
  });
  const tokenResponse = await fetch("https://oauth2.googleapis.com/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body,
  });
  if (!tokenResponse.ok) throw new RuleError(401, "auth code refused");
  const tokens = (await tokenResponse.json()) as { access_token?: string };
  if (!tokens.access_token) throw new RuleError(401, "auth code refused");

  const me = await fetch("https://games.googleapis.com/games/v1/players/me", {
    headers: { Authorization: "Bearer " + tokens.access_token },
  });
  if (!me.ok) throw new RuleError(401, "player not found");
  const player = (await me.json()) as { playerId?: string };
  if (!player.playerId) throw new RuleError(401, "player not found");
  return player.playerId;
}

/** Google Play's record of one in-app purchase. */
export async function getProductPurchase(packageName: string, productId: string, token: string): Promise<PlayPurchase> {
  const client = await auth.getClient();
  const url = `${PUBLISHER}/${encodeURIComponent(packageName)}/purchases/products/${encodeURIComponent(productId)}/tokens/${encodeURIComponent(token)}`;
  try {
    const res = await client.request<{ purchaseState?: number; orderId?: string; purchaseTimeMillis?: string }>({ url });
    return {
      purchaseState: res.data.purchaseState ?? 0,
      orderId: res.data.orderId ?? "",
      purchaseTimeMillis: Number(res.data.purchaseTimeMillis ?? 0),
    };
  } catch (error) {
    const status = (error as { response?: { status?: number } }).response?.status;
    if (status === 400 || status === 404 || status === 410) throw new RuleError(404, "purchase not found");
    throw error;
  }
}

/** Every in-app purchase voided (refunded, canceled, charged back) since <startMillis> (Google keeps 30 days). */
export async function listVoided(packageName: string, startMillis: number): Promise<VoidedPurchase[]> {
  const client = await auth.getClient();
  const url = `${PUBLISHER}/${encodeURIComponent(packageName)}/purchases/voidedpurchases`;
  const all: VoidedPurchase[] = [];
  let pageToken: string | undefined;
  do {
    const params: Record<string, string> = { startTime: String(startMillis), type: "0", maxResults: "1000" };
    if (pageToken) params.token = pageToken;
    const res = await client.request<{
      voidedPurchases?: Array<{ purchaseToken?: string; orderId?: string; voidedTimeMillis?: string; voidedReason?: number }>;
      tokenPagination?: { nextPageToken?: string };
    }>({ url, params });
    for (const v of res.data.voidedPurchases ?? []) {
      if (!v.purchaseToken) continue;
      all.push({
        purchaseToken: v.purchaseToken,
        orderId: v.orderId ?? "",
        voidedTimeMillis: Number(v.voidedTimeMillis ?? 0),
        voidedReason: v.voidedReason ?? 0,
      });
    }
    pageToken = res.data.tokenPagination?.nextPageToken;
  } while (pageToken);
  return all;
}
