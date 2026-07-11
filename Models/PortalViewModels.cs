namespace OpportunityHub.Models;

public class HomeViewModel
{
    public IReadOnlyList<Opportunity> Featured { get; set; } = [];
    public IReadOnlyDictionary<string, int> CategoryCounts { get; set; } = new Dictionary<string, int>();
    public int OpportunityCount { get; set; }
}

public class BrowseViewModel
{
    public IReadOnlyList<Opportunity> Opportunities { get; set; } = [];
    public string? Query { get; set; }
    public string? Location { get; set; }
    public string? WorkMode { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string Sort { get; set; } = "newest";
}

public class OpportunityDetailsViewModel
{
    public Opportunity Opportunity { get; set; } = new();
    public ApplicationInput Application { get; set; } = new();
    public IReadOnlyList<Opportunity> Similar { get; set; } = [];
    
    // Track if current candidate has already applied
    public bool HasApplied { get; set; }
}
