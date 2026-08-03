import { useEffect, useState } from "react";
import { apiBaseUrl, type ShipWishlistItem } from "./api";

export function useWishlist() {
  const [items, setItems] = useState<ShipWishlistItem[]>([]);

  const refetch = () => {
    fetch(`${apiBaseUrl}/api/fleet/wishlist`)
      .then((r) => r.json())
      .then(setItems)
      .catch(() => {});
  };

  useEffect(refetch, []);

  const add = async (shipType: string, displayName: string, estimatedCost: number | null) => {
    await fetch(`${apiBaseUrl}/api/fleet/wishlist`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ shipType, displayName, targetLoadoutNotes: null, estimatedCost, priority: 0 }),
    });
    refetch();
  };

  const markPurchased = async (id: number) => {
    await fetch(`${apiBaseUrl}/api/fleet/wishlist/${id}/purchased`, { method: "POST" });
    refetch();
  };

  const remove = async (id: number) => {
    await fetch(`${apiBaseUrl}/api/fleet/wishlist/${id}`, { method: "DELETE" });
    refetch();
  };

  return { items, add, markPurchased, remove };
}
