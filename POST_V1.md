# Post-v1

Work that didn't make the cut for v1. Pick anything from here when motivated; nothing is scheduled.

---

## Barcode scanning

Scan an EAN with the phone camera, auto-fill the Add dialog with name + likely store. After a few weeks of use, the family-local cache covers ~all recurring items.

**Why it's not in v1.** Austrian retail coverage from free data sources is uneven — Open Food Facts handles branded goods (Coca-Cola, Manner, Almdudler, Iglo) but is weak on Hofer "Zurück zum Ursprung" / Spar "S-Budget" / "Clever" / Baumarkt SKUs. There is no clean public bulk source for Austrian private labels. The pattern that works is **OFF as a one-time bootstrap + family-local cache as the real database** — but the cache needs real shopping to populate, so we shipped v1 first and let the family use it.

### Data model addition (single migration)

- `GroceryItem.Ean string?` — nullable, indexed.
- `BarcodeLookup { string Ean (PK), string Name, int? DefaultStoreId, int Count, DateTime LastUsedAt }` — family-local cache; one row per scanned EAN.

### Lookup pipeline

`IGroceryService.LookupBarcodeAsync(string ean)`:

1. **Local cache hit** → return `{ Name, DefaultStoreId, source: "local" }`, bump `Count`/`LastUsedAt`.
2. **Open Food Facts**: `GET https://world.openfoodfacts.org/api/v2/product/{ean}.json?fields=product_name,product_name_de,brands,image_front_small_url`. Typed `HttpClient`, 3s timeout. Return `{ Name = product_name_de ?? product_name ?? "" }`. Cache the result locally on first save so step 1 catches it next time.
3. **Miss** → return null. UI lets the user type the name; saving the item populates the EAN-to-name mapping.

### UI

- Scan icon next to the FAB on the Liste page (`MudIconButton`, `Icons.Material.Filled.QrCodeScanner`).
- Camera dialog: `BarcodeDetector` Web API on Chromium/Android, with `@zxing/browser` fallback for iOS Safari (which still doesn't expose `BarcodeDetector` natively). Tiny ES module via `IJSRuntime`.
- On detected EAN → `LookupBarcodeAsync` → prefill name + store → user confirms.
- Optional "An Open Food Facts senden" link on misses (deep-link to the OFF mobile app or `world.openfoodfacts.org` contribute page) so we become a contributor over time, not just a consumer.

### Constraints

- Camera access requires HTTPS. Works against the Fly URL or a dev tunnel; works against `localhost` (browsers grant camera on `localhost` even over HTTP). Does NOT work against `https://localhost` with a self-signed cert from a phone.
- iOS Safari is the awkward platform — test there before declaring done.
- Rate-limit OFF politely (one in-flight request, 5s debounce on repeated scans of the same code).

### Data sources rationale

What's usable, what isn't, and what's tempting-but-wrong:

- **Open Food Facts** (`world.openfoodfacts.org/api/v2/product/<ean>.json`) — free, crowdsourced, decent for branded food. Weak on Austrian private labels. The pragmatic baseline.
- **GS1 Austria** (`gs1.at`) is the authoritative registrar of EANs starting with `90`/`91`, but bulk access is paid B2B. The free GEPIR portal returns the brand owner of a single EAN, not the product name. Not usable for a family app.
- **Do not** scrape `spar.at` / `hofer.at` / `billa.at` — ToS violation, page structures change, fragile.
- **Do not** buy commercial barcode-lookup APIs (UPCitemdb, EAN-Search, Barcodelookup, Datakick) expecting Austrian private-label coverage — they're US/UK-centric and the private-label data simply isn't there.
- **The real database is the family-local `BarcodeLookup` cache** populated by first scans. After a few shopping trips a family's regular set (typically <150 SKUs) is fully covered. OFF is a one-time bootstrap per EAN, not a runtime dependency.

---

## Wishlist

Things we said no to for v1. Each is a small, self-contained project.

- **Drag-sort** for stores and favorites (currently up/down buttons).
- **Item edit** — currently delete-and-readd.
- **Push notifications** — "Papa hat Milch hinzugefügt".
- **`litestream` SQLite replication** to S3/B2 so the family list survives a Fly volume loss.
- **Per-user attribution colors** — colored dot next to "von David" / "von Mama" so attribution is visually scannable.
