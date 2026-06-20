# KusinaFlow — Panel Presentation Guide

A walkthrough of how the system actually works, for defending functionality
questions ("how does X connect to Y?", "walk me through what happens when...").
Each section names the real files/functions involved so you can pull them up
live if asked to show code.

---

## 1. System Architecture (the 30-second answer)

```
frontend/          → static HTML/CSS/JS, no framework, runs in the browser
  ├─ login/, dashboard/, stocks/, stock-history/, gen-reports/, staff-management/
  ├─ api.js         → every page's fetch wrapper (attaches the JWT)
  └─ style.css      → one shared stylesheet for the whole app

middleware/         → separate class library (KusinaFlows.Middleware)
  └─ JWT bearer authentication setup — issues and validates tokens

backend/            → ASP.NET Core Web API (KusinaFlows.Controllers)
  ├─ Controllers/    → HTTP in/out only (Auth, Inventory, Staff)
  ├─ Models/         → data shape + validation rules (no SQL)
  ├─ Repositories/   → all SQL lives here (Npgsql → Neon Postgres)
  └─ Services/       → DatabaseService (connection factory), PasswordHasher

Neon PostgreSQL      → ITEM, "STOCK HISTORY", "STOCK CONTROLLER" tables
```

**The request lifecycle, in one sentence:** the browser calls `fetch()` →
`api.js` attaches `Authorization: Bearer <jwt>` → the ASP.NET Core pipeline
validates the JWT (`middleware/JwtAuthExtensions.cs`) → the matching
Controller method runs → it calls a Repository method → the Repository runs
parameterized SQL against Postgres → the result flows back up the same chain
as JSON.

---

## 2. Login — how authentication actually works

**Frontend:** `frontend/login/login.js`
```js
const response = await fetch('http://localhost:5244/api/auth/login', {
   method: 'POST',
   headers: { 'Content-Type': 'application/json' },
   body: JSON.stringify({ username: username_input.value, password: password_input.value })
});
...
localStorage.setItem("authToken", data.token);
localStorage.setItem("currentUser", JSON.stringify(data.user));
```

**Backend:** `backend/Controllers/AuthController.cs` → `Login()`
1. Looks the user up via `IStaffRepository.FindByUsernameAsync(username)`.
2. Verifies the password with `PasswordHasher.Verify()` (PBKDF2 hash compare).
3. If the stored password was still plaintext (legacy row), it's silently
   re-hashed right there (`UpgradeLegacyPasswordAsync`) — self-healing, no
   migration script needed.
4. Issues a JWT: `_jwtTokenService.GenerateToken(scId, username, position)`.

**Every subsequent request:** `frontend/api.js`'s `kfFetch()` reads the token
out of `localStorage` and attaches it to every API call automatically:
```js
const token = getAuthToken();
const headers = new Headers(options.headers || {});
if (token && !headers.has("Authorization")) {
  headers.set("Authorization", `Bearer ${token}`);
}
```
On the backend, `[Authorize]` on `InventoryController`/`StaffController`
means ASP.NET Core's JWT middleware rejects any request without a valid
token *before it ever reaches the controller code*.

---

## 3. Add Item — full path from click to database row

**UI trigger:** `frontend/stocks/stocks.js`, `itemForm.addEventListener("submit", ...)`
- Client-side validation first (UTD can't be in the past, name required, etc.)
- Builds a payload and calls:
```js
const res = await window.kfFetch(`${API_BASE_URL}/inventory/add`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
});
```

**Backend controller:** `InventoryController.AddNewBatch()`
```csharp
if (!item.IsValid(out string validationError)) return BadRequest(validationError);
bool itemExists = await _repo.ItemNameExistsAsync(item.ItemName);
string action = itemExists ? "Stock-In" : "Add Item";
int batchId = await _repo.InsertBatchAsync(item, action, phNow);
await _repo.InsertHistoryAsync(new StockHistory { ... });
```
Two things happen on every add: a row goes into `ITEM` (the live inventory
table), **and** a row goes into `"STOCK HISTORY"` (the permanent audit log).
That's why every mutation anywhere in the app shows up in Stock History.

**Repository (the actual SQL):** `backend/Repositories/InventoryRepository.cs`
```csharp
public async Task<int> InsertBatchAsync(InventoryItem item, string action, string dateAdded)
{
    using var conn = _dbService.GetConnection();
    await conn.OpenAsync();
    using var cmd = new NpgsqlCommand(@"
        INSERT INTO public.""ITEM""
            (""ItemID"",""ItemName"",""Category"",""Price"",""Quantity"",
             ""Available"",""UTD"",""Action"",""DateAdded"")
        VALUES (@ItemID,@ItemName,@Category,@Price,@Quantity,true,@UTD,@Action,@DateAdded)
        RETURNING ""BatchID"";", conn);
    ...
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}
```

---

## 4. Edit Item — one edit, many batches

This is the one most likely to get a "how does that work" question, since
it's not a 1:1 edit.

**The rule:** an Item (e.g. "Coca-Cola") can have multiple Batches (different
deliveries, different expiry dates). Editing Name/Category/Price at the
**item** level should change all of them — not just one.

**Flow:**
1. `stocks.js` → `openEditPriceModal(itemId, itemName, category, price)` pre-fills the modal.
2. On submit, calls `PUT /api/inventory/update-item/{itemId}`.
3. `InventoryController.UpdateItem()`:
```csharp
var activeBatches = await _repo.GetActiveBatchesByItemIdAsync(itemId);
await _repo.UpdateItemAcrossBatchesAsync(itemId, dto.ItemName, dto.Category, dto.Price);

foreach (var batch in activeBatches)
{
    await _repo.InsertHistoryAsync(new StockHistory
    {
        BatchID = batch.BatchID,        // each batch's own ID
        OldPrice = batch.Price,          // each batch's own pre-edit price
        Price = dto.Price,               // the new shared price
        ...
    });
}
```
One SQL `UPDATE ... WHERE ItemID = @I AND Available = true` changes every
active batch in a single statement, then the loop writes **one history row
per batch** so Stock History shows the edit happened to every batch
individually (with each batch's own accurate "before" value), not just one
representative row.

---

## 5. Stock-Out — the deduction + guard rail

`InventoryController.StockOutSpecific()`:
```csharp
var batch = await _repo.GetBatchAsync(dto.BatchID);
if (dto.Quantity > batch.Quantity)
    return BadRequest($"Cannot deduct {dto.Quantity} from batch with only {batch.Quantity} units.");
int newQty = batch.Quantity - dto.Quantity;
await _repo.UpdateQuantityAsync(dto.BatchID, newQty, "Stock-Out", phNow);
```
You can't deduct more than what's in the batch — checked server-side, not
just in the UI, so it can't be bypassed by calling the API directly.

---

## 6. Authorization — who can do what

Three layers, from loosest to strictest:

| Layer | Where | What it checks |
|---|---|---|
| Authenticated at all | `[Authorize]` on the Controller | Valid, non-expired JWT |
| Manager/Owner only | `StaffController` action methods | `CallerPosition` claim from the JWT |
| Manager can't touch Owner/Manager | `StaffController.UpdateStaff()` | Looks up the *target* row's current Position before allowing the edit |

```csharp
private string CallerPosition => User.FindFirst("position")?.Value ?? "Staff";
private bool CallerIsManagerOrOwner => CallerPosition == "Manager" || CallerPosition == "Owner";
...
if (CallerPosition == "Manager")
{
    string? currentPosition = await _repo.GetPositionAsync(id);
    bool targetIsPrivileged = currentPosition == "Manager" || currentPosition == "Owner";
    bool payloadEscalates = payload.Position == "Manager" || payload.Position == "Owner";
    if (targetIsPrivileged || payloadEscalates) return Forbid();
}
```
This is enforced **server-side** specifically because the UI-only version
(hiding buttons, disabling dropdowns) can be bypassed by anyone who knows how
to open devtools or use curl — verified by literally doing that during
development and confirming it correctly returned 403.

---

## 7. Stock History / Recent Activity — the join that resolves names

Names shown in "Performed By" / "Approved By" aren't stored as text in
`"STOCK HISTORY"` — only `SC_ID` foreign keys are. The display name and role
are resolved live via SQL JOIN every time history is fetched:

```sql
SELECT h."SH_ID", h."BatchID", ..., 
    TRIM(CONCAT(p."FirstName", ' ', p."LastName")) AS PerformedByName,
    p."Position" AS PerformedByPosition
FROM public."STOCK HISTORY" h
LEFT JOIN public."STOCK CONTROLLER" p ON h."PerformedBy_SC_ID" = p."SC_ID"
LEFT JOIN public."STOCK CONTROLLER" a ON h."ApprovedBy_SC_ID"  = a."SC_ID"
```
This means if a staff member's name changes later, old history rows still
show the *current* name (since it's resolved at read-time, not stored at
write-time) — and if a staff record is ever deleted, the `LEFT JOIN` still
returns the history row with "System Auto"/"N/A" instead of breaking.

---

## 8. If asked "why is the database connection still working / how is it configured"

`backend/Services/DatabaseService.cs` wraps the Neon Postgres connection
string (from `appsettings.json`, under `ConnectionStrings:DefaultConnection`)
and hands out a fresh `NpgsqlConnection` to whoever asks for one. It's
registered once as a singleton in `Program.cs`:
```csharp
builder.Services.AddSingleton<KusinaFlows.Services.DatabaseService>();
```
Every Repository takes a `DatabaseService` in its constructor (constructor
injection) and calls `_dbService.GetConnection()` per method — so the
connection string itself only ever lives in one place.

---

## 9. Likely panel questions and short answers

- **"Why split into backend/middleware/frontend?"** Separation of concerns —
  auth logic (middleware) shouldn't live inside the API project, and the
  static frontend has zero build step, so it's naturally separate already.
- **"What happens if the JWT expires?"** The request comes back 401, the
  frontend's `kfFetch()` wrapper catches that and redirects to login
  automatically — no manual session checks scattered through every page.
- **"Why Repository pattern instead of SQL in the controller?"** Testability
  and separation — the Controller doesn't need to know it's Postgres, it
  just calls an interface method. See Section 10 for the actual file.
- **"How do you stop a regular Staff account from managing other staff?"**
  Both UI-hidden (`nav.js`) and server-enforced (`StaffController`'s
  `CallerIsManagerOrOwner` check) — UI hiding alone isn't real security,
  which is why both layers exist.

---

## 10. Quick file map for live code reference

| Functionality | Controller | Repository | Frontend |
|---|---|---|---|
| Login/JWT | `AuthController.cs` | `StaffRepository.cs` | `login/login.js` |
| Add/Edit/Delete inventory | `InventoryController.cs` | `InventoryRepository.cs` | `stocks/stocks.js` |
| Stock History | `InventoryController.GetStockHistory()` | `InventoryRepository.GetHistoryAsync()` | `stock-history/stock-history.js` |
| Staff CRUD | `StaffController.cs` | `StaffRepository.cs` | `staff-management/staff-management.js` |
| Reports | `InventoryController` (reused) | `InventoryRepository` (reused) | `gen-reports/reports.js` (client-side filtering) |

See `OOP_PILLARS.md` for the 4-pillars code reference specifically.
