namespace KusinaFlows.Models
{
    // No extra fields beyond the shared audit pair, so the base class's
    // IsValid() is sufficient as-is — not every derived type needs to
    // override the virtual method, only the ones with their own rules.
    public class DeleteRequestDto : AuditableRequest
    {
    }
}
