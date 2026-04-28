using System.Text.Json;

namespace Simply.JobApplication.Tests.M1;

// M1-2: Model defaults and serialization.
public class ModelDefaultsTests
{
    private static readonly JsonSerializerOptions _camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Organization_SerializeDeserialize_RoundTrip_PreservesAllFields()
    {
        var org = new Organization
        {
            Name = "ACME", Industry = "Tech", Size = "50", Website = "https://acme.io",
            LinkedIn = "https://linkedin.com/co/acme", Description = "desc",
        };
        var json = JsonSerializer.Serialize(org, _camel);
        var back = JsonSerializer.Deserialize<Organization>(json, _camel)!;

        Assert.Equal(org.Name,        back.Name);
        Assert.Equal(org.Industry,    back.Industry);
        Assert.Equal(org.Size,        back.Size);
        Assert.Equal(org.Website,     back.Website);
        Assert.Equal(org.LinkedIn,    back.LinkedIn);
        Assert.Equal(org.Description, back.Description);
    }

    [Fact]
    public void Opportunity_Stage_DefaultsToOpen()
        => Assert.Equal(OpportunityStage.Open, new Opportunity().Stage);

    [Fact]
    public void Opportunity_Version_DefaultsToOne()
        => Assert.Equal(1, new Opportunity().Version);

    [Fact]
    public void Correspondence_Direction_DefaultsToIncoming()
        => Assert.Equal(CorrespondenceDirection.Incoming, new Correspondence().Direction);

    [Fact]
    public void OpportunityFieldHistory_Changes_SerializesAsJsonArray()
    {
        var h = new OpportunityFieldHistory
        {
            Changes = new List<FieldChange>
            {
                new() { FieldName = "Role", OldValue = "Dev" },
            },
        };
        var json = JsonSerializer.Serialize(h, _camel);
        Assert.Contains("\"changes\"", json);
        Assert.Contains("\"fieldName\"", json);
    }

    [Fact]
    public void SessionRecord_ArtifactsGenerated_DefaultsFalse()
        => Assert.False(new SessionRecord().ArtifactsGenerated);

    [Fact]
    public void SessionRecord_SerializeDeserialize_PreservesAllPhase2Fields()
    {
        var s = new SessionRecord
        {
            OrganizationId            = "org-1",
            OrganizationNameSnapshot  = "ACME",
            OpportunityId             = "opp-1",
            OpportunityRoleSnapshot   = "Engineer",
            BaseResumeVersionId       = "v-1",
            BaseResumeNameSnapshot    = "My Resume",
            BaseResumeVersionNumberSnapshot = 2,
            ArtifactsGenerated        = true,
        };
        var json = JsonSerializer.Serialize(s, _camel);
        var back = JsonSerializer.Deserialize<SessionRecord>(json, _camel)!;

        Assert.Equal(s.OrganizationId,                   back.OrganizationId);
        Assert.Equal(s.OrganizationNameSnapshot,         back.OrganizationNameSnapshot);
        Assert.Equal(s.OpportunityId,                    back.OpportunityId);
        Assert.Equal(s.BaseResumeVersionId,              back.BaseResumeVersionId);
        Assert.Equal(s.BaseResumeNameSnapshot,           back.BaseResumeNameSnapshot);
        Assert.Equal(s.BaseResumeVersionNumberSnapshot,  back.BaseResumeVersionNumberSnapshot);
        Assert.True(back.ArtifactsGenerated);
    }

    [Fact]
    public void AppSettings_DoesNotDeclare_HistoryLimitProperty()
        => Assert.Null(typeof(AppSettings).GetProperty("HistoryLimit"));

    [Fact]
    public void AppSettings_DoesNotDeclare_FilesLimitProperty()
        => Assert.Null(typeof(AppSettings).GetProperty("FilesLimit"));

    [Fact]
    public void AllNewModels_SerializeWithCamelCasePropertyNames()
    {
        var types = new (object instance, string expectedKey)[]
        {
            (new Organization { Name = "X" },          "name"),
            (new Contact { FullName = "X" },            "fullName"),
            (new Opportunity { Role = "X" },            "role"),
            (new Correspondence { Subject = "X" },      "subject"),
            (new BaseResume { Name = "X" },             "name"),
            (new BaseResumeVersion { FileName = "X" },  "fileName"),
        };
        foreach (var (obj, key) in types)
        {
            var json = JsonSerializer.Serialize(obj, _camel);
            Assert.Contains($"\"{key}\"", json);
        }
    }
}
