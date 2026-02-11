namespace DigitalStampRally.Models;

public class StampRallyProjectExport
{
    public string App { get; set; } = $"{DigitalStampRally.Models.AppConst.AppNameEn}";
    public string Format { get; set; } = "stamp-rally-project";

    public int Version { get; set; } = 1;

    public string EventTitle { get; set; } = "";
    public EventImageRef? EventImage { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    public List<SpotExport> Spots { get; set; } = new();
}

public class SpotExport
{
    public string SpotName { get; set; } = "";
    public bool IsRequired { get; set; }
}

public class ProjectDto : StampRallyProjectExport
{
    public string EventId { get; set; } = "";

    new public EventImageDto? EventImage { get; set; }

    new public List<SpotDto> Spots { get; set; } = new();

    // ゴール用 / 集計用
    public string GoalToken { get; set; } = "";
    public string TotalizeToken { get; set; } = "";
    public string TotalizePassword { get; set; } = "";

    public UrlsDto Urls { get; set; } = new();
}

public class SpotDto : SpotExport
{
    public string SpotId { get; set; } = "";

    // MVP：spot用トークン（改ざん防止は後で署名方式に置換可）
    public string SpotToken { get; set; } = "";
}

public class UrlsDto
{
    public string ReadStampBase { get; set; } = "";
    public string SetGoalBase { get; set; } = "";
    public string TotalizeBase { get; set; } = "";
}

public class EventImageRef
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class EventImageDto
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string Base64 { get; set; } = "";
}
