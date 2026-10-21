/**
 * Gems each product gives. Must match Assets/Scripts/Shop/ShopCatalog.cs (the product ids created in
 * Google Play Console). A refunded purchase takes these gems back.
 */
export const GEMS_BY_PRODUCT: Readonly<Record<string, number>> = {
  "plorp.gems.20": 20,
  "plorp.gems.45": 45,
  "plorp.starter.150": 150,
  "plorp.gems.120": 120,
  "plorp.gems.260": 260,
  "plorp.gems.560": 560,
};

export function gemsFor(productId: string): number | undefined {
  return Object.prototype.hasOwnProperty.call(GEMS_BY_PRODUCT, productId) ? GEMS_BY_PRODUCT[productId] : undefined;
}
