using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using KusinaFlows.Models;
using KusinaFlows.Services;

namespace KusinaFlows.Repositories
{
    // Concrete Postgres implementation of IInventoryRepository — every raw SQL
    // statement that used to live in InventoryController now lives here.
    public class InventoryRepository : IInventoryRepository
    {
        private readonly DatabaseService _dbService;

        public InventoryRepository(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task<List<InventoryItem>> GetAllAsync(bool includeUnavailable)
        {
            var items = new List<InventoryItem>();

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

            using var cmd = new NpgsqlCommand(query, conn);
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

            return items;
        }

        public async Task<bool> ItemNameExistsAsync(string itemName)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT COUNT(1) FROM public.""ITEM"" WHERE ""ItemName""=@N;", conn);
            cmd.Parameters.AddWithValue("@N", itemName);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        public async Task<int> GetNextItemIdAsync()
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(
                @"SELECT COALESCE(MAX(""ItemID""),100)+1 FROM public.""ITEM"";", conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<int> InsertBatchAsync(InventoryItem item, string action, string dateAdded)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO public.""ITEM""
                    (""ItemID"",""ItemName"",""Category"",""Price"",""Quantity"",
                     ""Available"",""UTD"",""Action"",""DateAdded"")
                VALUES
                    (@ItemID,@ItemName,@Category,@Price,@Quantity,
                     true,@UTD,@Action,@DateAdded)
                RETURNING ""BatchID"";", conn);

            cmd.Parameters.AddWithValue("@ItemID",    item.ItemID);
            cmd.Parameters.AddWithValue("@ItemName",  item.ItemName);
            cmd.Parameters.AddWithValue("@Category",  item.Category);
            cmd.Parameters.AddWithValue("@Price",     item.Price);
            cmd.Parameters.AddWithValue("@Quantity",  item.Quantity);
            cmd.Parameters.AddWithValue("@UTD",       item.UTD);
            cmd.Parameters.AddWithValue("@Action",    action);
            cmd.Parameters.AddWithValue("@DateAdded", dateAdded);

            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        public async Task<InventoryItem?> GetBatchAsync(int batchId)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                SELECT ""Quantity"",""Price"",""UTD"",""Category"",""ItemName"",""Available""
                FROM public.""ITEM"" WHERE ""BatchID""=@B;", conn);
            cmd.Parameters.AddWithValue("@B", batchId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new InventoryItem
            {
                BatchID   = batchId,
                Quantity  = reader.GetInt32(0),
                Price     = reader.GetDecimal(1),
                UTD       = reader.GetInt32(2),
                Category  = reader.GetString(3),
                ItemName  = reader.GetString(4),
                Available = reader.GetBoolean(5)
            };
        }

        public async Task UpdateFullBatchAsync(InventoryItem item, string dateAdded)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                UPDATE public.""ITEM""
                SET ""ItemName""=@ItemName, ""Category""=@Category,
                    ""Price""=@Price, ""Quantity""=@Quantity, ""Available""=true,
                    ""UTD""=@UTD, ""Action""='Edit Item', ""DateAdded""=@DateAdded
                WHERE ""BatchID""=@B;", conn);

            cmd.Parameters.AddWithValue("@ItemName",  item.ItemName);
            cmd.Parameters.AddWithValue("@Category",  item.Category);
            cmd.Parameters.AddWithValue("@Price",     item.Price);
            cmd.Parameters.AddWithValue("@Quantity",  item.Quantity);
            cmd.Parameters.AddWithValue("@UTD",       item.UTD);
            cmd.Parameters.AddWithValue("@DateAdded", dateAdded);
            cmd.Parameters.AddWithValue("@B",         item.BatchID);

            await cmd.ExecuteNonQueryAsync();
        }

        // Returns EVERY active batch of the item, not just one representative
        // row — Edit Item needs an old-value snapshot per batch so each one
        // gets its own accurate Stock History entry after the bulk update.
        public async Task<List<InventoryItem>> GetActiveBatchesByItemIdAsync(int itemId)
        {
            var batches = new List<InventoryItem>();

            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                SELECT ""BatchID"",""Price"",""ItemName"",""Category"",""Quantity"",""UTD""
                FROM public.""ITEM"" WHERE ""ItemID""=@I AND ""Available""=true
                ORDER BY ""BatchID"" ASC;", conn);
            cmd.Parameters.AddWithValue("@I", itemId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                batches.Add(new InventoryItem
                {
                    BatchID  = reader.GetInt32(0),
                    Price    = reader.GetDecimal(1),
                    ItemName = reader.GetString(2),
                    Category = reader.GetString(3),
                    Quantity = reader.GetInt32(4),
                    UTD      = reader.GetInt32(5),
                    ItemID   = itemId
                });
            }

            return batches;
        }

        public async Task UpdateItemAcrossBatchesAsync(int itemId, string itemName, string category, decimal price)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                UPDATE public.""ITEM""
                SET ""ItemName""=@ItemName, ""Category""=@Category, ""Price""=@Price
                WHERE ""ItemID""=@I AND ""Available""=true;", conn);
            cmd.Parameters.AddWithValue("@ItemName", itemName);
            cmd.Parameters.AddWithValue("@Category", category);
            cmd.Parameters.AddWithValue("@Price", price);
            cmd.Parameters.AddWithValue("@I", itemId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateQuantityAsync(int batchId, int newQuantity, string action, string dateAdded)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                UPDATE public.""ITEM""
                SET ""Quantity""=@Q, ""Available""=true,
                    ""Action""=@Action, ""DateAdded""=@D
                WHERE ""BatchID""=@B;", conn);
            cmd.Parameters.AddWithValue("@Q", newQuantity);
            cmd.Parameters.AddWithValue("@Action", action);
            cmd.Parameters.AddWithValue("@D", dateAdded);
            cmd.Parameters.AddWithValue("@B", batchId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task SoftDeleteBatchAsync(int batchId, string dateAdded)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

            using var cmd = new NpgsqlCommand(@"
                UPDATE public.""ITEM""
                SET ""Available""=false, ""Action""='Deleted', ""DateAdded""=@D
                WHERE ""BatchID""=@B;", conn);
            cmd.Parameters.AddWithValue("@D", dateAdded);
            cmd.Parameters.AddWithValue("@B", batchId);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<object>> GetHistoryAsync()
        {
            var logs = new List<object>();

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

            using var cmd = new NpgsqlCommand(query, conn);
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
                    PerformedBy = reader.IsDBNull(13) ? "System Auto" : reader.GetString(13),
                    ApprovedBy  = reader.IsDBNull(14) ? "N/A"         : reader.GetString(14),
                    PerformedByPosition = reader.IsDBNull(15) ? null : reader.GetString(15),
                    ApprovedByPosition  = reader.IsDBNull(16) ? null : reader.GetString(16)
                });
            }

            return logs;
        }

        public async Task InsertHistoryAsync(StockHistory h)
        {
            using var conn = _dbService.GetConnection();
            await conn.OpenAsync();

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
            cmd.Parameters.AddWithValue("@PID", h.PerformedBy_SC_ID);
            cmd.Parameters.AddWithValue("@AID", h.ApprovedBy_SC_ID);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
