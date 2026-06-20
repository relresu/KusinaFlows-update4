namespace KusinaFlows.Models
{
    public class StockOutDto : AuditableRequest
    {
        public int BatchID { get; set; }
        public int Quantity { get; set; }

        public override bool IsValid(out string error)
        {
            if (!base.IsValid(out error)) return false;

            if (BatchID <= 0) { error = "A valid BatchID is required."; return false; }
            if (Quantity <= 0) { error = "Quantity must be greater than zero."; return false; }

            error = string.Empty;
            return true;
        }
    }
}
