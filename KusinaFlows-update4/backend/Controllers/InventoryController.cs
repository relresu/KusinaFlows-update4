using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KusinaFlows.Models;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KusinaFlows.Services;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // any authenticated staff member (Staff/Manager/Owner) can manage inventory
    public class InventoryController : ControllerBase
    {
        private readonly DatabaseService _dbService;

        private static readonly TimeZoneInfo PhilippineTime =
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        public InventoryController(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // ====================================================================
        // GET api/inventory
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> GetAllInventory([FromQuery] bool includeUnavailable = false)
        {
            var items = new List<InventoryItem>();
            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                string query = @"
                    SELECT
                        i.""BatchID"",
                        i.""ItemID"",
                        i.""ItemName"",
                        i.""Category"",
                        i.""Price"",
                        i.""Quantity"",
                        i.""Available"",
                        i.""UTD"",
                        i.""Action"",
                        i.""DateAdded""
                    FROM public.""ITEM"" i "
                    + (includeUnavailable ? "" : @"WHERE i.""Available"" = true ")
                    + @"ORDER BY i.""BatchID"" DESC;";

                using var cmd    = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    items.Add(new InventoryItem
                    {
                        BatchID   = reader.GetInt32(0),
                        ItemID    = reader.GetInt32(1),
                        ItemName  = reader.GetString(2),
                        Category  = reader.GetString(3),
                        Price     = reader.GetDecimal(4),
                        Quantity  = reader.GetInt32(5),
                        Available = reader.GetBoolean(6),
                        UTD       = reader.GetInt32(7),
                        Action    = reader.IsDBNull(8) ? "Add Item" : reader.GetString(8),
                        DateAdded = reader.IsDBNull(9) ? ""         : reader.GetString(9)
                    });
                }

                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve inventory: {ex.Message}");
            }
        }

        // ====================================================================
        // POST api/inventory/add
        // ====================================================================
        [HttpPost("add")]
        public async Task<IActionResult> AddNewBatch([FromBody] InventoryItem item)
        {
            if (item == null) return BadRequest("Invalid parameters.");
            if (string.IsNullOrWhiteSpace(item.ItemName)) return BadRequest("Item name is required.");
            if (item.Quantity < 0) return BadRequest("Quantity cannot be negative.");
            if (item.Price < 0) return BadRequest("Price cannot be negative.");
            if (item.PerformedByScId == null) return BadRequest("PerformedBy_SC_ID is required.");
            if (item.ApprovedByScId  == null) return BadRequest("ApprovedBy_SC_ID is required.");
            string phNow = GetPhilippineTimestamp();

            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                bool itemExists = false;
                using (var chk = new NpgsqlCommand(
                    @"SELECT COUNT(1) FROM public.""ITEM"" WHERE ""ItemName""=@N;", conn))
                {
                    chk.Parameters.AddWithValue("@N", item.ItemName);
                    itemExists = Convert.ToInt32(await chk.ExecuteScalarAsync()) > 0;
                }

                if (item.ItemID <= 0)
                {
                    using var mx = new NpgsqlCommand(
                        @"SELECT COALESCE(MAX(""ItemID""),100)+1 FROM public.""ITEM"";", conn);
                    item.ItemID = Convert.ToInt32(await mx.ExecuteScalarAsync());
                }

                string action = itemExists ? "Stock-In" : "Add Item";

                int batchId;
                using (var cmd = new NpgsqlCommand(@"
                    INSERT INTO public.""ITEM""
                        (""ItemID"",""ItemName"",""Category"",""Price"",""Quantity"",
                         ""Available"",""UTD"",""Action"",""DateAdded"")
                    VALUES
                        (@ItemID,@ItemName,@Category,@Price,@Quantity,
                         true,@UTD,@Action,@DateAdded)
                    RETURNING ""BatchID"";", conn))
                {
                    cmd.Parameters.AddWithValue("@ItemID",    item.ItemID);
                    cmd.Parameters.AddWithValue("@ItemName",  item.ItemName);
                    cmd.Parameters.AddWithValue("@Category",  item.Category);
                    cmd.Parameters.AddWithValue("@Price",     item.Price);
                    cmd.Parameters.AddWithValue("@Quantity",  item.Quantity);
                    cmd.Parameters.AddWithValue("@UTD",       item.UTD);
                    cmd.Parameters.AddWithValue("@Action",    action);
                    cmd.Parameters.AddWithValue("@DateAdded", phNow);
                    batchId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                await InsertHistoryAsync(conn, new StockHistory
                {
                    BatchID           = batchId,
                    ItemName          = item.ItemName,
                    Action            = action,
                    Quantity          = item.Quantity,
                    OldQuantity       = 0,
                    Price             = item.Price,
                    OldPrice          = item.Price,
                    UTD               = item.UTD,
                    OldUTD            = item.UTD,
                    Category          = item.Category,
                    OldCategory       = item.Category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = item.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = item.ApprovedByScId!.Value
                });

                return Ok(new { message = "Batch added successfully.", batchId });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ====================================================================
        // PUT api/inventory/update-full-batch
        // ====================================================================
        [HttpPut("update-full-batch")]
        public async Task<IActionResult> UpdateFullBatch([FromBody] InventoryItem item)
        {
            if (item == null || item.BatchID <= 0) return BadRequest("Invalid parameters.");
            if (string.IsNullOrWhiteSpace(item.ItemName)) return BadRequest("Item name is required.");
            if (item.Quantity < 0) return BadRequest("Quantity cannot be negative.");
            if (item.Price < 0) return BadRequest("Price cannot be negative.");
            if (item.PerformedByScId == null) return BadRequest("PerformedBy_SC_ID is required.");
            if (item.ApprovedByScId  == null) return BadRequest("ApprovedBy_SC_ID is required.");
            string phNow = GetPhilippineTimestamp();

            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                int oldQty = 0; decimal oldPrice = 0; int oldUtd = 0; string oldCat = "";
                using (var sel = new NpgsqlCommand(@"
                    SELECT ""Quantity"",""Price"",""UTD"",""Category""
                    FROM public.""ITEM"" WHERE ""BatchID""=@B;", conn))
                {
                    sel.Parameters.AddWithValue("@B", item.BatchID);
                    using var r = await sel.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                    {
                        oldQty=r.GetInt32(0); oldPrice=r.GetDecimal(1);
                        oldUtd=r.GetInt32(2); oldCat=r.GetString(3);
                    }
                }

                using (var cmd = new NpgsqlCommand(@"
                    UPDATE public.""ITEM""
                    SET ""ItemName""=@ItemName, ""Category""=@Category,
                        ""Price""=@Price, ""Quantity""=@Quantity, ""Available""=true,
                        ""UTD""=@UTD, ""Action""='Edit Item', ""DateAdded""=@DateAdded
                    WHERE ""BatchID""=@B;", conn))
                {
                    cmd.Parameters.AddWithValue("@ItemName",  item.ItemName);
                    cmd.Parameters.AddWithValue("@Category",  item.Category);
                    cmd.Parameters.AddWithValue("@Price",     item.Price);
                    cmd.Parameters.AddWithValue("@Quantity",  item.Quantity);
                    cmd.Parameters.AddWithValue("@UTD",       item.UTD);
                    cmd.Parameters.AddWithValue("@DateAdded", phNow);
                    cmd.Parameters.AddWithValue("@B",         item.BatchID);
                    await cmd.ExecuteNonQueryAsync();
                }

                await InsertHistoryAsync(conn, new StockHistory
                {
                    BatchID           = item.BatchID,
                    ItemName          = item.ItemName,
                    Action            = "Edit Item",
                    Quantity          = item.Quantity,
                    OldQuantity       = oldQty,
                    Price             = item.Price,
                    OldPrice          = oldPrice,
                    UTD               = item.UTD,
                    OldUTD            = oldUtd,
                    Category          = item.Category,
                    OldCategory       = oldCat,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = item.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = item.ApprovedByScId!.Value
                });

                return Ok(new { message = "Batch updated successfully." });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ====================================================================
        // PUT api/inventory/update-price/{itemId}
        // Updates the price across all active (Available=true) batches of an item
        // ====================================================================
        [HttpPut("update-price/{itemId}")]
        public async Task<IActionResult> UpdateItemPrice(int itemId, [FromBody] UpdatePriceDto dto)
        {
            if (dto == null || dto.Price < 0) return BadRequest("Invalid price.");
            if (dto.PerformedByScId == null) return BadRequest("PerformedBy_SC_ID is required.");
            if (dto.ApprovedByScId  == null) return BadRequest("ApprovedBy_SC_ID is required.");
            string phNow = GetPhilippineTimestamp();

            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                int firstBatchId = 0; decimal oldPrice = 0; string itemName = ""; string category = "";
                int firstQty = 0; int firstUtd = 0;

                using (var sel = new NpgsqlCommand(@"
                    SELECT ""BatchID"",""Price"",""ItemName"",""Category"",""Quantity"",""UTD""
                    FROM public.""ITEM"" WHERE ""ItemID""=@I AND ""Available""=true
                    ORDER BY ""BatchID"" ASC;", conn))
                {
                    sel.Parameters.AddWithValue("@I", itemId);
                    using var r = await sel.ExecuteReaderAsync();
                    if (!await r.ReadAsync()) return NotFound("No active batches found for this item.");
                    firstBatchId = r.GetInt32(0);
                    oldPrice     = r.GetDecimal(1);
                    itemName     = r.GetString(2);
                    category     = r.GetString(3);
                    firstQty     = r.GetInt32(4);
                    firstUtd     = r.GetInt32(5);
                }

                using (var upd = new NpgsqlCommand(@"
                    UPDATE public.""ITEM"" SET ""Price""=@P
                    WHERE ""ItemID""=@I AND ""Available""=true;", conn))
                {
                    upd.Parameters.AddWithValue("@P", dto.Price);
                    upd.Parameters.AddWithValue("@I", itemId);
                    await upd.ExecuteNonQueryAsync();
                }

                await InsertHistoryAsync(conn, new StockHistory
                {
                    BatchID           = firstBatchId,
                    ItemName          = itemName,
                    Action            = "Edit Price",
                    Quantity          = firstQty,
                    OldQuantity       = firstQty,
                    Price             = dto.Price,
                    OldPrice          = oldPrice,
                    UTD               = firstUtd,
                    OldUTD            = firstUtd,
                    Category          = category,
                    OldCategory       = category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = dto.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = dto.ApprovedByScId!.Value
                });

                return Ok(new { message = "Price updated successfully." });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ====================================================================
        // GET api/inventory/all-history
        // Joins STOCK HISTORY → STOCK CONTROLLER via FK to resolve staff names
        // ====================================================================
        [HttpGet("all-history")]
        public async Task<IActionResult> GetStockHistory()
        {
            var logs = new List<object>();
            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                // PerformedBy and ApprovedBy VARCHAR columns are dropped.
                // Names are resolved entirely via FK JOIN to STOCK CONTROLLER.
                string query = @"
                    SELECT
                        h.""SH_ID"",
                        h.""BatchID"",
                        h.""ItemName"",
                        h.""Action"",
                        h.""DateTime"",
                        h.""Quantity"",
                        h.""OldQuantity"",
                        h.""Price"",
                        h.""OldPrice"",
                        h.""UTD"",
                        h.""OldUTD"",
                        h.""Category"",
                        h.""OldCategory"",
                        TRIM(CONCAT(p.""FirstName"", ' ', p.""LastName"")) AS PerformedByName,
                        TRIM(CONCAT(a.""FirstName"", ' ', a.""LastName"")) AS ApprovedByName,
                        p.""Position"" AS PerformedByPosition,
                        a.""Position"" AS ApprovedByPosition
                    FROM public.""STOCK HISTORY"" h
                    LEFT JOIN public.""STOCK CONTROLLER"" p ON h.""PerformedBy_SC_ID"" = p.""SC_ID""
                    LEFT JOIN public.""STOCK CONTROLLER"" a ON h.""ApprovedBy_SC_ID""  = a.""SC_ID""
                    ORDER BY h.""SH_ID"" DESC;";

                using var cmd    = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    logs.Add(new
                    {
                        SH_ID       = reader.GetInt32(0),
                        BatchID     = reader.GetInt32(1),
                        ItemName    = reader.IsDBNull(2)  ? "Unknown" : reader.GetString(2),
                        Action      = reader.IsDBNull(3)  ? "Add Item": reader.GetString(3),
                        DateTime    = reader.IsDBNull(4)  ? "N/A"     : reader.GetString(4),
                        Quantity    = reader.GetInt32(5),
                        OldQuantity = reader.IsDBNull(6)  ? 0         : reader.GetInt32(6),
                        Price       = reader.IsDBNull(7)  ? 0m        : reader.GetDecimal(7),
                        OldPrice    = reader.IsDBNull(8)  ? 0m        : reader.GetDecimal(8),
                        UTD         = reader.IsDBNull(9)  ? 0         : reader.GetInt32(9),
                        OldUTD      = reader.IsDBNull(10) ? 0         : reader.GetInt32(10),
                        Category    = reader.IsDBNull(11) ? "General" : reader.GetString(11),
                        OldCategory = reader.IsDBNull(12) ? "General" : reader.GetString(12),
                        // Columns 13 & 14 — resolved via FK JOIN; null if staff record was deleted
                        PerformedBy = reader.IsDBNull(13) ? "System Auto" : reader.GetString(13),
                        ApprovedBy  = reader.IsDBNull(14) ? "N/A"         : reader.GetString(14),
                        // Columns 15 & 16 — staff Position resolved via the same FK JOIN
                        PerformedByPosition = reader.IsDBNull(15) ? null : reader.GetString(15),
                        ApprovedByPosition  = reader.IsDBNull(16) ? null : reader.GetString(16)
                    });
                }

                return Ok(logs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HISTORY FETCH ERROR]: {ex.Message}");
                return StatusCode(500, $"Failed to retrieve history: {ex.Message}");
            }
        }

        // ====================================================================
        // POST api/inventory/stock-out-specific
        // ====================================================================
        [HttpPost("stock-out-specific")]
        public async Task<IActionResult> StockOutSpecific([FromBody] StockOutDto dto)
        {
            if (dto == null || dto.BatchID <= 0 || dto.Quantity <= 0)
                return BadRequest("Invalid stock-out parameters.");
            if (dto.PerformedByScId == null) return BadRequest("PerformedBy_SC_ID is required.");
            if (dto.ApprovedByScId  == null) return BadRequest("ApprovedBy_SC_ID is required.");
            string phNow = GetPhilippineTimestamp();

            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                int oldQty=0; decimal price=0; int utd=0;
                string category="", itemName="";

                using (var sel = new NpgsqlCommand(@"
                    SELECT ""Quantity"",""Price"",""UTD"",""Category"",""ItemName""
                    FROM public.""ITEM"" WHERE ""BatchID""=@B;", conn))
                {
                    sel.Parameters.AddWithValue("@B", dto.BatchID);
                    using var r = await sel.ExecuteReaderAsync();
                    if (!await r.ReadAsync()) return NotFound($"Batch #{dto.BatchID} not found.");
                    oldQty=r.GetInt32(0); price=r.GetDecimal(1); utd=r.GetInt32(2);
                    category=r.GetString(3); itemName=r.GetString(4);
                }

                if (dto.Quantity > oldQty)
                    return BadRequest($"Cannot deduct {dto.Quantity} from batch with only {oldQty} units.");

                int newQty = oldQty - dto.Quantity;

                using (var upd = new NpgsqlCommand(@"
                    UPDATE public.""ITEM""
                    SET ""Quantity""=@Q, ""Available""=true,
                        ""Action""='Stock-Out', ""DateAdded""=@D
                    WHERE ""BatchID""=@B;", conn))
                {
                    upd.Parameters.AddWithValue("@Q", newQty);
                    upd.Parameters.AddWithValue("@D", phNow);
                    upd.Parameters.AddWithValue("@B", dto.BatchID);
                    await upd.ExecuteNonQueryAsync();
                }

                await InsertHistoryAsync(conn, new StockHistory
                {
                    BatchID           = dto.BatchID,
                    ItemName          = itemName,
                    Action            = "Stock-Out",
                    Quantity          = newQty,
                    OldQuantity       = oldQty,
                    Price             = price,
                    OldPrice          = price,
                    UTD               = utd,
                    OldUTD            = utd,
                    Category          = category,
                    OldCategory       = category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = dto.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = dto.ApprovedByScId!.Value
                });

                return Ok(new { message = $"Stock-Out: Batch #{dto.BatchID} {oldQty}→{newQty}" });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ====================================================================
        // DELETE api/inventory/delete/{batchId}
        // ====================================================================
        [HttpDelete("delete/{batchId}")]
        public async Task<IActionResult> DeleteBatch(int batchId, [FromBody] DeleteRequestDto? dto)
        {
            if (batchId <= 0) return BadRequest("Invalid BatchID.");
            if (dto?.PerformedByScId == null) return BadRequest("PerformedBy_SC_ID is required.");
            if (dto?.ApprovedByScId  == null) return BadRequest("ApprovedBy_SC_ID is required.");
            string phNow = GetPhilippineTimestamp();

            try
            {
                using var conn = _dbService.GetConnection();
                await conn.OpenAsync();

                int qty=0; decimal price=0; int utd=0;
                string category="", itemName="";
                bool alreadyDeleted=false;

                using (var sel = new NpgsqlCommand(@"
                    SELECT ""Quantity"",""Price"",""UTD"",""Category"",""ItemName"",""Available""
                    FROM public.""ITEM"" WHERE ""BatchID""=@B;", conn))
                {
                    sel.Parameters.AddWithValue("@B", batchId);
                    using var r = await sel.ExecuteReaderAsync();
                    if (!await r.ReadAsync()) return NotFound($"Batch #{batchId} not found.");
                    qty=r.GetInt32(0); price=r.GetDecimal(1); utd=r.GetInt32(2);
                    category=r.GetString(3); itemName=r.GetString(4);
                    alreadyDeleted=!r.GetBoolean(5);
                }

                if (alreadyDeleted)
                    return Ok(new { message = $"Batch #{batchId} already archived." });

                using (var upd = new NpgsqlCommand(@"
                    UPDATE public.""ITEM""
                    SET ""Available""=false, ""Action""='Deleted', ""DateAdded""=@D
                    WHERE ""BatchID""=@B;", conn))
                {
                    upd.Parameters.AddWithValue("@D", phNow);
                    upd.Parameters.AddWithValue("@B", batchId);
                    await upd.ExecuteNonQueryAsync();
                }

                await InsertHistoryAsync(conn, new StockHistory
                {
                    BatchID           = batchId,
                    ItemName          = itemName,
                    Action            = "Deleted",
                    Quantity          = 0,
                    OldQuantity       = qty,
                    Price             = price,
                    OldPrice          = price,
                    UTD               = utd,
                    OldUTD            = utd,
                    Category          = category,
                    OldCategory       = category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = dto!.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = dto!.ApprovedByScId!.Value
                });

                return Ok(new { message = $"Batch #{batchId} archived." });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        // ====================================================================
        // PRIVATE HELPERS
        // ====================================================================

        private static async Task InsertHistoryAsync(NpgsqlConnection conn, StockHistory h)
        {
            using var cmd = new NpgsqlCommand(@"
                INSERT INTO public.""STOCK HISTORY""
                    (""BatchID"",""ItemName"",""Action"",""DateTime"",
                     ""Quantity"",""OldQuantity"",
                     ""Price"",""OldPrice"",
                     ""UTD"",""OldUTD"",
                     ""Category"",""OldCategory"",
                     ""PerformedBy_SC_ID"",""ApprovedBy_SC_ID"")
                VALUES
                    (@BatchID,@ItemName,@Action,@DateTime,
                     @Quantity,@OldQuantity,
                     @Price,@OldPrice,
                     @UTD,@OldUTD,
                     @Category,@OldCategory,
                     @PID,@AID);", conn);

            cmd.Parameters.AddWithValue("@BatchID",     h.BatchID);
            cmd.Parameters.AddWithValue("@ItemName",    h.ItemName);
            cmd.Parameters.AddWithValue("@Action",      h.Action);
            cmd.Parameters.AddWithValue("@DateTime",    h.DateTime);
            cmd.Parameters.AddWithValue("@Quantity",    h.Quantity);
            cmd.Parameters.AddWithValue("@OldQuantity", h.OldQuantity);
            cmd.Parameters.AddWithValue("@Price",       h.Price);
            cmd.Parameters.AddWithValue("@OldPrice",    h.OldPrice);
            cmd.Parameters.AddWithValue("@UTD",         h.UTD);
            cmd.Parameters.AddWithValue("@OldUTD",      h.OldUTD);
            cmd.Parameters.AddWithValue("@Category",    h.Category);
            cmd.Parameters.AddWithValue("@OldCategory", h.OldCategory);
            // NOT NULL constraint — values guaranteed non-null by endpoint validation above
            cmd.Parameters.AddWithValue("@PID", h.PerformedBy_SC_ID);
            cmd.Parameters.AddWithValue("@AID", h.ApprovedBy_SC_ID);

            await cmd.ExecuteNonQueryAsync();
        }

        private string GetPhilippineTimestamp() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PhilippineTime)
                        .ToString("yyyy-MM-dd HH:mm:ss");
    }
}