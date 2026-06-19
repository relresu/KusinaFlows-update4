namespace KusinaFlows.Models
{
    public class InventoryItem
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

        // SC_IDs from the logged-in user and selected approver.
        // NOT NULL in STOCK HISTORY — validated before any history insert.
        public int? PerformedByScId { get; set; }
        public int? ApprovedByScId { get; set; }
    }
}