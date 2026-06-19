namespace KusinaFlows.Models
{
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
    }
}