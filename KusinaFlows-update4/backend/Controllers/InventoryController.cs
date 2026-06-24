using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KusinaFlows.Models;
using KusinaFlows.Repositories;
using System;
using System.Threading.Tasks;

namespace KusinaFlows.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // any authenticated staff member (Staff/Manager/Owner) can manage inventory
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryRepository _repo;

        private static readonly TimeZoneInfo PhilippineTime =
            TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        // ABSTRACTION: the controller is wired to the interface, not the
        // concrete InventoryRepository — see Program.cs's DI registration.
        public InventoryController(IInventoryRepository repo)
        {
            _repo = repo;
        }

        // ====================================================================
        // GET api/inventory
        // ====================================================================
        [HttpGet]
        public async Task<IActionResult> GetAllInventory([FromQuery] bool includeUnavailable = false)
        {
            try
            {
                return Ok(await _repo.GetAllAsync(includeUnavailable));
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
            // POLYMORPHISM: IsValid() here runs InventoryItem's override (item
            // name / quantity / price rules layered on top of the shared
            // PerformedBy/ApprovedBy check from AuditableRequest).
            if (!item.IsValid(out string validationError)) return BadRequest(validationError);

            string phNow = GetPhilippineTimestamp();

            try
            {
                bool itemExists = await _repo.ItemNameExistsAsync(item.ItemName);
                if (item.ItemID <= 0)
                {
                    item.ItemID = await _repo.GetNextItemIdAsync();
                }

                string action = itemExists ? "Stock-In" : "Add Item";
                int batchId = await _repo.InsertBatchAsync(item, action, phNow);

                await _repo.InsertHistoryAsync(new StockHistory
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
            if (!item.IsValid(out string validationError)) return BadRequest(validationError);

            string phNow = GetPhilippineTimestamp();

            try
            {
                var existing = await _repo.GetBatchAsync(item.BatchID);
                int oldQty = existing?.Quantity ?? 0;
                decimal oldPrice = existing?.Price ?? 0;
                int oldUtd = existing?.UTD ?? 0;
                string oldCat = existing?.Category ?? "";

                await _repo.UpdateFullBatchAsync(item, phNow);

                await _repo.InsertHistoryAsync(new StockHistory
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
        // PUT api/inventory/update-item/{itemId}
        // Edits apply at the item level (Name/Category/Price) and affect every
        // active batch of that item — each batch gets its own Stock History
        // row reflecting its own old values, not just one representative entry.
        // ====================================================================
        [HttpPut("update-item/{itemId}")]
        public async Task<IActionResult> UpdateItem(int itemId, [FromBody] UpdateItemDto dto)
        {
            if (dto == null) return BadRequest("Invalid parameters.");
            if (!dto.IsValid(out string validationError)) return BadRequest(validationError);

            string phNow = GetPhilippineTimestamp();

            try
            {
                var activeBatches = await _repo.GetActiveBatchesByItemIdAsync(itemId);
                if (activeBatches.Count == 0) return NotFound("No active batches found for this item.");

                await _repo.UpdateItemAcrossBatchesAsync(itemId, dto.ItemName, dto.Category, dto.Price);

                // One history row per affected batch, each carrying its own
                // pre-edit snapshot — so every available batch genuinely shows
                // up in Stock History, not just a single stand-in row.
                foreach (var batch in activeBatches)
                {
                    await _repo.InsertHistoryAsync(new StockHistory
                    {
                        BatchID           = batch.BatchID,
                        ItemName          = dto.ItemName,
                        Action            = "Edit Item",
                        Quantity          = batch.Quantity,
                        OldQuantity       = batch.Quantity,
                        Price             = dto.Price,
                        OldPrice          = batch.Price,
                        UTD               = batch.UTD,
                        OldUTD            = batch.UTD,
                        Category          = dto.Category,
                        OldCategory       = batch.Category,
                        DateTime          = phNow,
                        PerformedBy_SC_ID = dto.PerformedByScId!.Value,
                        ApprovedBy_SC_ID  = dto.ApprovedByScId!.Value
                    });
                }

                return Ok(new { message = $"Item updated across {activeBatches.Count} batch(es)." });
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
            try
            {
                return Ok(await _repo.GetHistoryAsync());
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
            if (dto == null) return BadRequest("Invalid stock-out parameters.");
            if (!dto.IsValid(out string validationError)) return BadRequest(validationError);

            string phNow = GetPhilippineTimestamp();

            try
            {
                var batch = await _repo.GetBatchAsync(dto.BatchID);
                if (batch == null) return NotFound($"Batch #{dto.BatchID} not found.");

                if (dto.Quantity > batch.Quantity)
                    return BadRequest($"Cannot deduct {dto.Quantity} from batch with only {batch.Quantity} units.");

                int newQty = batch.Quantity - dto.Quantity;
                await _repo.UpdateQuantityAsync(dto.BatchID, newQty, "Stock-Out", phNow);

                await _repo.InsertHistoryAsync(new StockHistory
                {
                    BatchID           = dto.BatchID,
                    ItemName          = batch.ItemName,
                    Action            = "Stock-Out",
                    Quantity          = newQty,
                    OldQuantity       = batch.Quantity,
                    Price             = batch.Price,
                    OldPrice          = batch.Price,
                    UTD               = batch.UTD,
                    OldUTD            = batch.UTD,
                    Category          = batch.Category,
                    OldCategory       = batch.Category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = dto.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = dto.ApprovedByScId!.Value
                });

                return Ok(new { message = $"Stock-Out: Batch #{dto.BatchID} {batch.Quantity}→{newQty}" });
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
            if (dto == null) return BadRequest("Invalid parameters.");
            if (!dto.IsValid(out string validationError)) return BadRequest(validationError);

            string phNow = GetPhilippineTimestamp();

            try
            {
                var batch = await _repo.GetBatchAsync(batchId);
                if (batch == null) return NotFound($"Batch #{batchId} not found.");

                if (!batch.Available)
                    return Ok(new { message = $"Batch #{batchId} already archived." });

                await _repo.SoftDeleteBatchAsync(batchId, phNow);

                await _repo.InsertHistoryAsync(new StockHistory
                {
                    BatchID           = batchId,
                    ItemName          = batch.ItemName,
                    Action            = "Deleted",
                    Quantity          = 0,
                    OldQuantity       = batch.Quantity,
                    Price             = batch.Price,
                    OldPrice          = batch.Price,
                    UTD               = batch.UTD,
                    OldUTD            = batch.UTD,
                    Category          = batch.Category,
                    OldCategory       = batch.Category,
                    DateTime          = phNow,
                    PerformedBy_SC_ID = dto.PerformedByScId!.Value,
                    ApprovedBy_SC_ID  = dto.ApprovedByScId!.Value
                });

                return Ok(new { message = $"Batch #{batchId} archived." });
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        private string GetPhilippineTimestamp() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PhilippineTime)
                        .ToString("yyyy-MM-dd HH:mm:ss");
    }
}
