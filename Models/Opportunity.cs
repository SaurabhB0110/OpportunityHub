namespace OpportunityHub.Models;

public class Opportunity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string WorkMode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Compensation { get; set; } = string.Empty;
    public string Experience { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Responsibilities { get; set; } = [];
    public List<string> Requirements { get; set; } = [];
    public List<string> Skills { get; set; } = [];
    public DateTime PostedAt { get; set; }
    public DateTime ApplyBy { get; set; }
    public int Applicants { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActivelyHiring { get; set; } = true;
    public string LogoColor { get; set; } = "#635bff";

    public string Initials => string.Concat(Company.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => x[0])).ToUpperInvariant();
    public string PostedLabel
    {
        get
        {
            var days = Math.Max(0, (DateTime.Today - PostedAt.Date).Days);
            return days == 0 ? "Today" : days == 1 ? "1 day ago" : $"{days} days ago";
        }
    }
}
