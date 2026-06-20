namespace KusinaFlows.Models
{
    // Backs the Stocks page's "Edit Item" action — edits apply at the item
    // level (Name/Category/Price), not to a single batch, so every active
    // batch of that item is updated and gets its own Stock History entry.
    public class UpdateItemDto : AuditableRequest
    {
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }

        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (string.IsNullOrWhiteSpace(ItemName)) { error = "Item name is required."; return false; }
            if (string.IsNullOrWhiteSpace(Category)) { error = "Category is required."; return false; }
            if (Price < 0) { error = "Price cannot be negative."; return false; }

            error = string.Empty;
            return true;
        }
    }
}
