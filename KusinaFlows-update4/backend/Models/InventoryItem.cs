namespace KusinaFlows.Models
{
    // ENCAPSULATION: the rules for what makes an item "valid" (name required,
    // no negative price/quantity) live inside the class that owns that data,
    // instead of being scattered as inline ifs in whatever controller happens
    // to receive one.
    public class InventoryItem : AuditableRequest
    {
        public int BatchID { get; set; }
        public int ItemID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int UTD { get; set; }
        public bool Available { get; set; } = true;
        public string Action { get; set; } = "Add Item";
        public string DateAdded { get; set; } = string.Empty;

        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false; // shared PerformedBy/ApprovedBy check

            if (string.IsNullOrWhiteSpace(ItemName)) { error = "Item name is required."; return false; }
            if (Quantity < 0) { error = "Quantity cannot be negative."; return false; }
            if (Price < 0) { error = "Price cannot be negative."; return false; }

            error = string.Empty;
            return true;
        }
    }
}
