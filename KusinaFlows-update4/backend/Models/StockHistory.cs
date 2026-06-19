namespace KusinaFlows.Models
{
    public class StockHistory
    {
        public int SH_ID { get; set; }
        public int BatchID { get; set; }
        public string DateTime { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int OldQuantity { get; set; }
        public decimal Price { get; set; }
        public decimal OldPrice { get; set; }
        public int UTD { get; set; }
        public int OldUTD { get; set; }
        public string Category { get; set; } = string.Empty;
        public string OldCategory { get; set; } = string.Empty;

        // NOT NULL in DB — must always be provided before inserting
        public int PerformedBy_SC_ID { get; set; }
        public int ApprovedBy_SC_ID { get; set; }
    }
}