using System.Collections.Generic;
using System.Threading.Tasks;
using KusinaFlows.Models;

namespace KusinaFlows.Repositories
{
    // ABSTRACTION: the controller talks to this interface only — it has no
    // idea Postgres/Npgsql is involved underneath. Swapping the database
    // engine, or substituting a fake implementation for tests, means writing
    // a new class against this contract; nothing in InventoryController changes.
    public interface IInventoryRepository
    {
        Task<List<InventoryItem>> GetAllAsync(bool includeUnavailable);
        Task<bool> ItemNameExistsAsync(string itemName);
        Task<int> GetNextItemIdAsync();
        Task<int> InsertBatchAsync(InventoryItem item, string action, string dateAdded);
        Task<InventoryItem?> GetBatchAsync(int batchId);
        Task UpdateFullBatchAsync(InventoryItem item, string dateAdded);
        Task<List<InventoryItem>> GetActiveBatchesByItemIdAsync(int itemId);
        Task UpdateItemAcrossBatchesAsync(int itemId, string itemName, string category, decimal price);
        Task UpdateQuantityAsync(int batchId, int newQuantity, string action, string dateAdded);
        Task SoftDeleteBatchAsync(int batchId, string dateAdded);
        Task<List<object>> GetHistoryAsync();
        Task InsertHistoryAsync(StockHistory history);
    }
}
