using System.Text.RegularExpressions;

namespace KusinaFlows.Models
{
    // ENCAPSULATION: name-format rules (letters only — no digits/symbols) are
    // owned by this class via IsValid(), not duplicated as inline regex checks
    // wherever a StaffDto happens to be handled.
    public class StaffDto
    {
        public int SC_ID { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MI { get; set; }
        public string? Position { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool Active { get; set; } = true;
        public string? ContactInfo { get; set; }
        public string? ProfilePicture { get; set; }
        public string? DateHired { get; set; }
        public string? LastLogin { get; set; }

        private static readonly Regex NamePattern = new(@"^[A-Za-z\s'-]+$");
        private static readonly Regex MiddleInitialPattern = new(@"^[A-Za-z.]*$");

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(FirstName) || !NamePattern.IsMatch(FirstName))
            {
                error = "First name must contain letters only (no numbers or symbols).";
                return false;
            }
            if (string.IsNullOrWhiteSpace(LastName) || !NamePattern.IsMatch(LastName))
            {
                error = "Last name must contain letters only (no numbers or symbols).";
                return false;
            }
            if (!string.IsNullOrEmpty(MI) && !MiddleInitialPattern.IsMatch(MI))
            {
                error = "Middle initial must contain letters only.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
