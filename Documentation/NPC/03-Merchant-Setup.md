# 03 — Merchant Setup

**Scripts:** `NPC/Merchant/Merchant.cs`, `MerchantStock.cs`, `MerchantPricing.cs`.

Step-by-step recipes. The shop window is in [04](04-Shop-Window-and-Custom-UI.md); money, categories and how
transactions are made safe in [05](05-Money-Categories-and-Transactions.md).

---

## 1. A merchant

1. **GameObject ▸ SimpleMovements ▸ NPC ▸ Merchant NPC** creates a placeholder merchant in front of the scene view.
   To use your character model, select it in the scene and use *Tools ▸ SimpleMovements ▸ NPC ▸ Create Merchant NPC
   (or make the selection one)*: a capsule collider (when it has none), an `NPC` and a `Merchant` are added.
   `Add Component ▸ SimpleMovements ▸ NPC ▸ Merchant` on any object works too (the `NPC` comes with it).
2. The player needs a **Player Interactor** — offered when you create the NPC, or *Tools ▸ SimpleMovements ▸ NPC ▸ Add
   Player Interactor To Players* (also adds it to selected player prefabs).
3. On the `NPC`: Display Name, Title, Portrait, greetings ([02](02-NPCs-and-Dialogue.md)).
4. On the `Merchant`: goods (§2), prices (§4), what it buys (§5). Done.

*Tools ▸ SimpleMovements ▸ NPC ▸ Validate NPCs In Scene* lists every problem (missing collider, layers the interactor
does not search, empty goods, items without a price or icon, duplicate ids...). The inspectors show the same problems.

## 2. Goods and stock

Each **Goods** entry is one line of the shop:

| Field | Meaning |
|---|---|
| Item | The item asset (any `ItemSO`: weapons, armor, consumables, ammo, materials...). |
| Unlimited | Never runs out. |
| Quantity | Stock at the start. |
| Max Quantity | Restock target (0 = Quantity). |
| Restock Amount | How many come back at each restock (0 = refilled to the maximum). |
| Price Multiplier | This item's price at this merchant (×1.5 = dearer). |
| Fixed Price | Exact price, whatever the value and the pricing (0 = calculated). |
| Category Override | List the item under another category in this shop. |

Inspector tools: **Add Selected Items** (select item assets in the Project window with the inspector locked), **Add
Items From Folder…**, **Sort By Category**, **Remove Empty**, **Save As Stock Asset…**. The **Shop preview** lists every
item with its tab, buy price and sell price.

**Shared stock:** *Assets ▸ Create ▸ SimpleMovements ▸ NPC ▸ Merchant Stock* (or *Save As Stock Asset…*) and assign it
to *Stock Asset* on several merchants; each merchant still has its own runtime quantities and can add its own entries.

**Restocking:** *Restock Every (minutes)* (0 = never). Limited goods come back; *Clear Sold Items On Restock* forgets
what the player sold. In Play Mode the inspector has *Restock Now* and *Reset Stock*; from code `merchant.Restock()`,
`merchant.RebuildStock()`, `merchant.Inventory.SetQuantity(slot, n)` (loading a save).

## 3. Buying (the player)

* The player picks an item (click), chooses a **quantity** for stackable items (−, +, typing, **Max** = as many as are
  in stock, affordable and fit in the bag) and presses **Buy**. **Double click** or **right click** buys one.
* It is refused, with the reason shown, when the player cannot pay, it is sold out, or the bag has no room — the space
  check uses the inventory itself, so it follows **slot mode** (free slots, existing stacks) and **grid mode** (the
  item's Grid Size must fit, turned when allowed) and the hotbar.
* On success the money is taken, the items are added with `InventoryManager.AddItem` (stacking onto existing stacks,
  grid placement, weight), and the stock goes down.

## 4. Prices (`Pricing`)

| Setting | Default | Meaning |
|---|---|---|
| Buy Multiplier | 1 | Player pays: item **Price** (its value) × this × the entry's Price Multiplier. |
| Sell Multiplier | 0.4 | Player gets: item Price × this. |
| Minimum Buy Price | 1 | Nothing costs less. |
| Sell Price Follows Durability | on | A worn item sells for less (the unit in use); *Broken Item Value* is what a broken one still fetches. |
| Sell Never Above Buy | on | The merchant never pays more than it charges for the same item — no buy-sell money loops. |
| Rounding | Nearest | Prices are whole amounts. |
| Modifiers | | Rules applied in order (pick the type): **Category** (a smith pays well for ores), **Item**, **Item Kind** (all materials), **Scarcity** (dearer as stock runs out), or your own (§6). |

## 5. Selling (the player)

The Sell tab shows the player's bag and hotbar (laid out like their inventory, [04](04-Shop-Window-and-Custom-UI.md)).
The player selects a stack, a quantity (stackable items) and presses **Sell**.

| Setting | Default | Meaning |
|---|---|---|
| Buys Items | on | Off = no Sell tab. |
| Buys Categories | (all) | Only items of these categories (and their subcategories). |
| Refused Items | | Never bought. |
| Refuse Worthless Items | on | Items whose selling price is 0 are refused. |
| Resell Bought Items | on | Sold items appear in the shop (the player can buy them back at the normal price). *Max Buyback Lines* limits how many different ones are kept. |

Never sold: items with **Can Be Sold** off (quest items: `MiscItemSO ▸ Quest Item`), the merchant's own coin item,
and anything **worn** in an equipment slot (unequip it first). The weapon in hand (hotbar) can be sold; the hand updates.

## 6. Code

```csharp
// Prices and rules
int pay  = merchant.GetBuyPrice(slot, player);
int get  = merchant.GetSellPrice(stack, player);
bool ok  = merchant.WillBuy(item, out string why);

// Trading without the window (a quest reward shop, a test)
var request = MerchantTransactionRequest.Buy(merchant, player, merchant.Inventory.Slots[0], 2);
merchant.Submit(request, result => Debug.Log(result.Message));   // "Bought 2 Potion for 40 g."

// A pricing rule of your own (appears in the Modifiers dropdown)
[System.Serializable]
public class ReputationDiscount : MerchantPriceModifier
{
    public float discountPerLevel = 0.05f;
    public override float Modify(float price, in MerchantPriceContext c)
    {
        int level = /* read the player's reputation from c.Player */ 0;
        return c.PlayerBuys ? price * (1f - discountPerLevel * level) : price;
    }
}

// Events
merchant.TransactionCompleted += r => { if (r.Succeeded) Debug.Log(r.Message); };
Merchant.AnyTransaction += (m, r) => { /* quests: "sell 5 wolf pelts" */ };
```

UnityEvents *On Item Bought* / *On Item Sold* are on the merchant for designers.

## 7. Checklist

1. NPC with a collider on a layer the Player Interactor searches.
2. Player Interactor on the player; an `Interact` action in the input actions (or the default E is used).
3. Goods with items that have a **Price** and an **Icon**.
4. Optional: category database and default currency in Resources ([05](05-Money-Categories-and-Transactions.md)),
   a Merchant UI Skin ([04](04-Shop-Window-and-Custom-UI.md)).
5. *Validate NPCs In Scene* reports nothing.
