namespace KusinaFlows.Models
{
    // Base for every inventory request that carries an audit trail (who
    // performed the action, who approved it). Every STOCK HISTORY row needs
    // both IDs, so this is shared by InventoryItem, StockOutDto,
    // DeleteRequestDto, and UpdatePriceDto instead of each redeclaring it.
    //
    // INHERITANCE: the four request models below extend this class.
    // POLYMORPHISM: IsValid() is virtual — each derived class overrides it to
    // layer its own rules on top of the shared audit-field check, and calling
    // code that only holds an AuditableRequest reference still runs whichever
    // override matches the object's real type.
    public abstract class AuditableRequest
    {
        public int? PerformedByScId { get; set; }
        public int? ApprovedByScId { get; set; }

        public virtual bool IsValid(out string error)
        {
            if (PerformedByScId == null) { error = "PerformedBy_SC_ID is required."; return false; }
            if (ApprovedByScId == null) { error = "ApprovedBy_SC_ID is required."; return false; }
            error = string.Empty;
            return true;
        }
    }
}
