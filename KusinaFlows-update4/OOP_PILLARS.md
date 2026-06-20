# The 4 Pillars of OOP in KusinaFlow's Backend

Concrete code, not just definitions. Each pillar below has multiple real
excerpts from the codebase, with file paths so they can be pulled up live.

---

## 1. Inheritance

**File: `backend/Models/AuditableRequest.cs`** — the shared base class.
```csharp
public abstract class AuditableRequest
{
    public int? PerformedByScId { get; set; }
    public int? ApprovedByScId { get; set; }

    public virtual bool IsValid(out string error)
    {
        if (PerformedByScId == null) { error = "PerformedBy_SC_ID is required."; return false; }
        if (ApprovedByScId == null) { error = "ApprovedBy_SC_ID is required."; return false; }
        error = string.Empty;
        return true;
    }
}
```

**Four different models all extend it** instead of redeclaring the same two
fields:
```csharp
// backend/Models/InventoryItem.cs
public class InventoryItem : AuditableRequest { ... }

// backend/Models/StockOutDto.cs
public class StockOutDto : AuditableRequest { ... }

// backend/Models/DeleteRequestDto.cs
public class DeleteRequestDto : AuditableRequest { }   // uses the base as-is

// backend/Models/UpdateItemDto.cs
public class UpdateItemDto : AuditableRequest { ... }
```

---

## 2. Polymorphism

**The mechanism:** `IsValid()` is declared `virtual` on the base class and
`override`n by each derived class, layering its own rules on top of the
shared check.

```csharp
// backend/Models/InventoryItem.cs
public override bool IsValid(out string error)
{
    if (!base.IsValid(out error)) return false;          // shared check first
    if (string.IsNullOrWhiteSpace(ItemName)) { error = "Item name is required."; return false; }
    if (Quantity < 0) { error = "Quantity cannot be negative."; return false; }
    if (Price < 0) { error = "Price cannot be negative."; return false; }
    error = string.Empty;
    return true;
}
```

```csharp
// backend/Models/StockOutDto.cs
public override bool IsValid(out string error)
{
    if (!base.IsValid(out error)) return false;
    if (BatchID <= 0) { error = "A valid BatchID is required."; return false; }
    if (Quantity <= 0) { error = "Quantity must be greater than zero."; return false; }
    error = string.Empty;
    return true;
}
```

**The polymorphic call site** — this is the important part. The controller
code is identical regardless of which model type it's holding:
```csharp
// backend/Controllers/InventoryController.cs — three different actions,
// three different concrete types, same line of calling code:
if (!item.IsValid(out string validationError)) return BadRequest(validationError);   // AddNewBatch (InventoryItem)
if (!dto.IsValid(out string validationError))  return BadRequest(validationError);   // StockOutSpecific (StockOutDto)
if (!dto.IsValid(out string validationError))  return BadRequest(validationError);   // UpdateItem (UpdateItemDto)
```
At compile time the parameter type is `AuditableRequest`-compatible; at run
time, .NET dispatches to whichever `override` matches the object's actual
class. That's polymorphism — one call shape, different behavior per type.

---

## 3. Encapsulation

**File: `backend/Services/PasswordHasher.cs`** — the hashing algorithm,
iteration count, and salt size are all `private`; nothing outside this class
can see or touch them.
```csharp
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password) { ... }
    public static bool Verify(string suppliedPassword, string storedPassword) { ... }
}
```
Callers only ever see `Hash()` and `Verify()` — the *how* is fully hidden.

**File: `backend/Models/StaffDto.cs`** — validation rules travel with the
data they validate, instead of being copy-pasted into every controller that
touches a `StaffDto`:
```csharp
public class StaffDto
{
    ...
    private static readonly Regex NamePattern = new(@"^[A-Za-z\s'-]+$");

    public bool IsValid(out string error)
    {
        if (string.IsNullOrWhiteSpace(FirstName) || !NamePattern.IsMatch(FirstName))
        {
            error = "First name must contain letters only (no numbers or symbols).";
            return false;
        }
        ...
    }
}
```
`StaffController` never inlines a regex — it just asks the object:
```csharp
if (!payload.IsValid(out string nameError))
    return BadRequest(new { message = nameError });
```

---

## 4. Abstraction

**File: `backend/Repositories/IInventoryRepository.cs`** — the contract the
rest of the app codes against. No SQL, no Npgsql, no connection details —
just what operations exist:
```csharp
public interface IInventoryRepository
{
    Task<List<InventoryItem>> GetAllAsync(bool includeUnavailable);
    Task<int> InsertBatchAsync(InventoryItem item, string action, string dateAdded);
    Task<InventoryItem?> GetBatchAsync(int batchId);
    Task UpdateItemAcrossBatchesAsync(int itemId, string itemName, string category, decimal price);
    Task SoftDeleteBatchAsync(int batchId, string dateAdded);
    Task InsertHistoryAsync(StockHistory history);
    // ...
}
```

**The concrete implementation** (`InventoryRepository.cs`) is where all the
actual SQL lives — this is the *only* file that knows Postgres is involved:
```csharp
public class InventoryRepository : IInventoryRepository
{
    private readonly DatabaseService _dbService;

    public async Task<InventoryItem?> GetBatchAsync(int batchId)
    {
        using var conn = _dbService.GetConnection();
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(@"
            SELECT ""Quantity"",""Price"",""UTD"",""Category"",""ItemName"",""Available""
            FROM public.""ITEM"" WHERE ""BatchID""=@B;", conn);
        ...
    }
}
```

**Wired together via Dependency Injection** in `Program.cs`:
```csharp
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IStaffRepository, StaffRepository>();
```

**The controller only ever sees the interface:**
```csharp
public class InventoryController : ControllerBase
{
    private readonly IInventoryRepository _repo;   // interface, not InventoryRepository

    public InventoryController(IInventoryRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllInventory([FromQuery] bool includeUnavailable = false)
    {
        return Ok(await _repo.GetAllAsync(includeUnavailable));   // no SQL, no idea it's Postgres
    }
}
```
If the database were ever swapped (different engine, or a fake
implementation for automated tests), only a new class implementing
`IInventoryRepository` would need to be written — `InventoryController.cs`
would not change at all.

---

## One-paragraph summary (if asked to explain in your own words)

`AuditableRequest` is the parent class four request models **inherit** from.
Each of those models **overrides** the shared `IsValid()` method to add its
own rules — that's **polymorphism**, since the same method call resolves
differently depending on the object's real type. `PasswordHasher` and
`StaffDto` **encapsulate** their internals (hashing details, name-validation
regex) behind a small public surface so nothing else needs to know how those
rules work. `IInventoryRepository`/`IStaffRepository` are **abstractions** —
Controllers depend on the interface, never the concrete Postgres-backed
class, so the data-access detail is fully decoupled from request handling.
