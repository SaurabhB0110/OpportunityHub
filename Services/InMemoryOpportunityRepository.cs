using System.Collections.Concurrent;
using OpportunityHub.Models;

namespace OpportunityHub.Services;

public class InMemoryOpportunityRepository : IOpportunityRepository
{
    private readonly List<Opportunity> _opportunities = Seed();
    private readonly ConcurrentBag<(int OpportunityId, ApplicationInput Application)> _applications = [];
    private readonly object _lock = new();

    public IReadOnlyList<Opportunity> GetAll()
    {
        lock (_lock) return _opportunities.Select(Clone).ToList();
    }

    public Opportunity? GetById(int id)
    {
        lock (_lock) return _opportunities.Where(x => x.Id == id).Select(Clone).FirstOrDefault();
    }

    public Opportunity Add(CreateOpportunityInput input)
    {
        lock (_lock)
        {
            var item = new Opportunity
            {
                Id = _opportunities.Max(x => x.Id) + 1,
                Title = input.Title.Trim(), Company = input.Company.Trim(), Location = input.Location.Trim(),
                WorkMode = input.WorkMode, Type = input.Type, Category = input.Category,
                Compensation = input.Compensation.Trim(), Experience = input.Experience.Trim(),
                Description = input.Description.Trim(), ApplyBy = input.ApplyBy, PostedAt = DateTime.Today,
                Skills = input.Skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Responsibilities = ["Collaborate with the team on meaningful projects", "Take ownership of assigned tasks and share progress"],
                Requirements = ["Strong communication and problem-solving skills", "A curious, proactive approach to learning"],
                LogoColor = "#ff6b4a", IsFeatured = false
            };
            _opportunities.Insert(0, item);
            return Clone(item);
        }
    }

    public void AddApplication(int opportunityId, ApplicationInput input)
    {
        _applications.Add((opportunityId, input));
        lock (_lock)
        {
            var item = _opportunities.FirstOrDefault(x => x.Id == opportunityId);
            if (item is not null) item.Applicants++;
        }
    }

    private static Opportunity Clone(Opportunity x) => new()
    {
        Id = x.Id, Title = x.Title, Company = x.Company, Location = x.Location, WorkMode = x.WorkMode,
        Type = x.Type, Category = x.Category, Compensation = x.Compensation, Experience = x.Experience,
        Description = x.Description, Responsibilities = [.. x.Responsibilities], Requirements = [.. x.Requirements],
        Skills = [.. x.Skills], PostedAt = x.PostedAt, ApplyBy = x.ApplyBy, Applicants = x.Applicants,
        IsFeatured = x.IsFeatured, IsActivelyHiring = x.IsActivelyHiring, LogoColor = x.LogoColor
    };

    private static List<Opportunity> Seed() =>
    [
        new() { Id=1, Title="Product Design Intern", Company="PixelCraft Studio", Location="Bengaluru", WorkMode="Hybrid", Type="Internship", Category="UI/UX Design", Compensation="₹18,000 / month", Experience="Fresher", Description="Join our design team to shape simple, delightful digital products used by fast-growing startups. You will work alongside senior designers from discovery through polished UI.", Responsibilities=["Create wireframes, prototypes, and production-ready interfaces", "Participate in user research and design critiques", "Maintain and extend our component library"], Requirements=["A portfolio that demonstrates thoughtful design decisions", "Comfort with Figma and visual design fundamentals", "Clear communication and an eagerness to learn"], Skills=["Figma","UI/UX","Prototyping"], PostedAt=DateTime.Today.AddDays(-1), ApplyBy=DateTime.Today.AddDays(18), Applicants=42, IsFeatured=true, LogoColor="#6c5ce7" },
        new() { Id=2, Title="Frontend Developer", Company="NovaStack Labs", Location="Remote", WorkMode="Remote", Type="Full-time", Category="Web Development", Compensation="₹7–10 LPA", Experience="1–3 years", Description="Build fast, accessible web experiences for a new generation of collaborative software. You will own features end-to-end in a small and ambitious engineering team.", Responsibilities=["Ship responsive product experiences using React and TypeScript", "Collaborate with design and backend engineering", "Improve performance, accessibility, and test coverage"], Requirements=["Strong JavaScript, HTML, and CSS fundamentals", "Experience with a modern frontend framework", "Care for product quality and maintainable code"], Skills=["React","TypeScript","CSS"], PostedAt=DateTime.Today.AddDays(-2), ApplyBy=DateTime.Today.AddDays(24), Applicants=67, IsFeatured=true, LogoColor="#0984e3" },
        new() { Id=3, Title="Marketing & Growth Intern", Company="GreenBasket", Location="Mumbai", WorkMode="On-site", Type="Internship", Category="Digital Marketing", Compensation="₹15,000 / month", Experience="Fresher", Description="Help grow a sustainable grocery brand through sharp content, partnerships, and experiments across digital channels.", Responsibilities=["Plan and publish social content", "Support creator and community partnerships", "Track campaign performance and share insights"], Requirements=["Strong writing and communication", "Interest in consumer brands and sustainability", "Comfort working with spreadsheets and social platforms"], Skills=["Content","Social Media","Analytics"], PostedAt=DateTime.Today.AddDays(-3), ApplyBy=DateTime.Today.AddDays(15), Applicants=31, IsFeatured=true, LogoColor="#00b894" },
        new() { Id=4, Title="Data Analyst", Company="FinEdge", Location="Gurugram", WorkMode="Hybrid", Type="Full-time", Category="Data Science", Compensation="₹8–12 LPA", Experience="1–2 years", Description="Turn complex financial data into clear insights that guide product and business decisions across the company.", Responsibilities=["Build dashboards and recurring performance reports", "Partner with teams to define useful metrics", "Investigate trends and communicate actionable findings"], Requirements=["Strong SQL and spreadsheet skills", "Experience with a BI or visualization tool", "Structured thinking and attention to detail"], Skills=["SQL","Power BI","Python"], PostedAt=DateTime.Today.AddDays(-1), ApplyBy=DateTime.Today.AddDays(21), Applicants=55, IsFeatured=true, LogoColor="#e17055" },
        new() { Id=5, Title="Human Resources Intern", Company="PeopleFirst Co.", Location="Pune", WorkMode="On-site", Type="Internship", Category="Human Resources", Compensation="₹12,000 / month", Experience="Fresher", Description="Get hands-on exposure to recruiting, onboarding, and employee experience in a people-first workplace.", Responsibilities=["Coordinate interviews and candidate communication", "Support onboarding and employee engagement", "Keep recruitment records organized"], Requirements=["Friendly, professional communication", "Strong organization and follow-through", "Interest in people operations"], Skills=["Recruiting","Communication","Excel"], PostedAt=DateTime.Today.AddDays(-5), ApplyBy=DateTime.Today.AddDays(12), Applicants=24, LogoColor="#fd79a8" },
        new() { Id=6, Title="Backend Engineer", Company="CloudMint", Location="Hyderabad", WorkMode="Hybrid", Type="Full-time", Category="Software Development", Compensation="₹12–18 LPA", Experience="2–4 years", Description="Design reliable services and APIs that power a growing cloud finance platform serving thousands of businesses.", Responsibilities=["Design and build scalable .NET services", "Review code and improve engineering practices", "Monitor performance and production reliability"], Requirements=["Professional experience with C# and ASP.NET Core", "Understanding of SQL and API design", "Experience with cloud infrastructure is a plus"], Skills=["C#","ASP.NET Core","SQL"], PostedAt=DateTime.Today.AddDays(-4), ApplyBy=DateTime.Today.AddDays(26), Applicants=39, LogoColor="#00cec9" },
        new() { Id=7, Title="Content Writer", Company="Storyline Media", Location="Remote", WorkMode="Remote", Type="Part-time", Category="Content Writing", Compensation="₹25,000 / month", Experience="0–2 years", Description="Write clear, useful stories for technology and lifestyle brands while learning from an experienced editorial team.", Responsibilities=["Research and write original articles", "Edit content to match brand voice", "Work with editors to meet weekly deadlines"], Requirements=["Excellent written English", "Strong online research habits", "Two or more relevant writing samples"], Skills=["Writing","SEO","Research"], PostedAt=DateTime.Today.AddDays(-6), ApplyBy=DateTime.Today.AddDays(14), Applicants=48, LogoColor="#a29bfe" },
        new() { Id=8, Title="Business Development Associate", Company="LaunchPad", Location="Delhi", WorkMode="On-site", Type="Full-time", Category="Business Analysis", Compensation="₹5–8 LPA + incentives", Experience="0–2 years", Description="Build relationships with startup founders and help them choose the right tools to grow their companies.", Responsibilities=["Qualify inbound leads and run discovery calls", "Maintain a healthy sales pipeline", "Share customer feedback with product teams"], Requirements=["Confident verbal and written communication", "Goal-oriented, resilient working style", "Prior customer-facing experience is useful"], Skills=["Sales","CRM","Negotiation"], PostedAt=DateTime.Today.AddDays(-2), ApplyBy=DateTime.Today.AddDays(20), Applicants=28, LogoColor="#f39c12" }
    ];
}
