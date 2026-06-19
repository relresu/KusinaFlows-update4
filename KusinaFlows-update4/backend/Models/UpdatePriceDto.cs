namespace KusinaFlows.Models
{
    public class UpdatePriceDto
    {
        public decimal Price { get; set; }
        public int? PerformedByScId { get; set; }
        public int? ApprovedByScId { get; set; }
    }
}
