namespace DigitalStampRally.Models;

public class ProjectDto
{
    public int Version { get; set; } = 1;

    public string EventId { get; set; } = "";
    public string EventTitle { get; set; } = "";

    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public EventImageDto? EventImage { get; set; }

    public List<SpotDto> Spots { get; set; } = new();

    // ゴール用 / 集計用
    public string GoalToken { get; set; } = "";
    public string TotalizeToken { get; set; } = "";
    public string TotalizePassword { get; set; } = "";

    public UrlsDto Urls { get; set; } = new();
}

public class SpotDto
{
    public string SpotId { get; set; } = "";
    public string SpotName { get; set; } = "";
    public bool IsRequired { get; set; }

    // MVP：spot用トークン（改ざん防止は後で署名方式に置換可）
    public string SpotToken { get; set; } = "";
}

public class UrlsDto
{
    public string ReadStampBase { get; set; } = "";
    public string SetGoalBase { get; set; } = "";
    public string TotalizeBase { get; set; } = "";
}

public class EventImageDto
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Base64 { get; set; } = "";
}
