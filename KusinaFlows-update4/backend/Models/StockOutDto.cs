namespace KusinaFlows.Models
{
    public class StockOutDto
    {
        public int BatchID { get; set; }
        public int Quantity { get; set; }
        public int? PerformedByScId { get; set; }
        public int? ApprovedByScId { get; set; }
    }
}