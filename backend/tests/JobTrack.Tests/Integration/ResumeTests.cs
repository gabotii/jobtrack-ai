using System.Net;
using System.Text;
using JobTrack.Application.JobOffers;
using JobTrack.Application.Resumes;
using JobTrack.Domain.Entities;
using JobTrack.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ResumeTests(ApiFactory factory)
{
    [Fact]
    public async Task Docx_and_pdf_uploads_extract_text_and_can_be_downloaded()
    {
        var user = await factory.RegisterUserAsync();

        var docx = await user.Client.PostAsync("/api/v1/resumes", ResumeFixtures.Upload(ResumeFixtures.Docx(), "Mi CV.docx"));
        await docx.EnsureStatusAsync(HttpStatusCode.Created);
        var pdfBytes = ResumeFixtures.Pdf();
        var pdf = await user.Client.PostAsync("/api/v1/resumes", ResumeFixtures.Upload(pdfBytes, "cv.pdf", "text/plain"));
        await pdf.EnsureStatusAsync(HttpStatusCode.Created);

        var created = await pdf.ReadAsync<ResumeDto>();
        Assert.Equal("application/pdf", created.ContentType); // detected from content, not from the client header

        var detail = await (await user.Client.GetAsync($"/api/v1/resumes/{created.Id}")).ReadAsync<ResumeDetailDto>();
        Assert.Contains("ASP.NET Core", detail.ExtractedText);

        var download = await user.Client.GetAsync($"/api/v1/resumes/{created.Id}/file");
        await download.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(pdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);

        var list = await (await user.Client.GetAsync("/api/v1/resumes")).ReadAsync<List<ResumeDto>>();
        Assert.Equal(2, list.Count);
    }

    [Theory]
    [InlineData("cv.txt", "texto plano")]
    [InlineData("fake.pdf", "esto no es un pdf aunque lo diga la extension")]
    [InlineData("fake.docx", "%PDF-1.7 un pdf disfrazado de docx")]
    public async Task Invalid_files_are_rejected(string fileName, string content)
    {
        var user = await factory.RegisterUserAsync();

        var response = await user.Client.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(Encoding.UTF8.GetBytes(content), fileName));

        await response.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Files_without_extractable_text_are_rejected()
    {
        var user = await factory.RegisterUserAsync();
        var response = await user.Client.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(ResumeFixtures.Docx("Hola"), "corto.docx"));

        await response.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("resume.no_text", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Oversized_uploads_are_rejected()
    {
        var user = await factory.RegisterUserAsync();
        var big = new byte[ResumeService.MaxFileBytes + 1];
        "%PDF-"u8.CopyTo(big);

        var response = await user.Client.PostAsync("/api/v1/resumes", ResumeFixtures.Upload(big, "big.pdf"));

        Assert.True(response.StatusCode is HttpStatusCode.UnprocessableEntity or HttpStatusCode.RequestEntityTooLarge,
            $"Unexpected status {(int)response.StatusCode}");
    }

    [Fact]
    public async Task The_same_file_cannot_be_uploaded_twice()
    {
        var user = await factory.RegisterUserAsync();
        var bytes = ResumeFixtures.Docx();
        await (await user.Client.PostAsync("/api/v1/resumes", ResumeFixtures.Upload(bytes, "a.docx"))).EnsureStatusAsync(HttpStatusCode.Created);

        var again = await user.Client.PostAsync("/api/v1/resumes", ResumeFixtures.Upload(bytes, "b.docx"));

        await again.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Another_user_cannot_read_download_analyse_or_delete_a_resume()
    {
        var owner = await factory.RegisterUserAsync();
        var intruder = await factory.RegisterUserAsync();
        var resume = await (await owner.Client.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(ResumeFixtures.Docx(), "cv.docx"))).ReadAsync<ResumeDto>();
        var intruderOffer = await intruder.Client.CreateOfferAsync();

        await (await intruder.Client.GetAsync($"/api/v1/resumes/{resume.Id}")).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.GetAsync($"/api/v1/resumes/{resume.Id}/file")).EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await intruder.Client.DeleteAsync($"/api/v1/resumes/{resume.Id}")).EnsureStatusAsync(HttpStatusCode.NotFound);
        var analyse = await intruder.Client.PostJsonAsync("/api/v1/resume-analyses",
            new CreateResumeAnalysisRequest(resume.Id, intruderOffer.Id, "local", false));
        await analyse.EnsureStatusAsync(HttpStatusCode.NotFound);
        Assert.Empty(await (await intruder.Client.GetAsync("/api/v1/resumes")).ReadAsync<List<ResumeDto>>());

        // Nor can another user's resume be attached to one's own application.
        var foreignResumeLink = await intruder.Client.PostJsonAsync("/api/v1/applications",
            new Application.Applications.CreateApplicationRequest(intruderOffer.Id, ApplicationStatus.Applied, null,
                resume.Id, null, null, null, null, null));
        await foreignResumeLink.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Local_analysis_is_stored_with_evidence_and_listed_in_history()
    {
        var user = await factory.RegisterUserAsync();
        var resume = await (await user.Client.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(ResumeFixtures.Docx(), "cv.docx"))).ReadAsync<ResumeDto>();
        var company = await user.Client.CreateCompanyAsync();
        var offer = await (await user.Client.PostJsonAsync("/api/v1/jobs", TestHelpers.OfferRequest(company.Id) with
        {
            Description = "Buscamos backend con C# y PostgreSQL. Valorable Kubernetes y Docker.",
            RequiredSkills = ["C#", "PostgreSQL", "Java"],
            PreferredSkills = ["Docker", "Kubernetes"],
        })).ReadAsync<JobOfferDto>();

        var response = await user.Client.PostJsonAsync("/api/v1/resume-analyses",
            new CreateResumeAnalysisRequest(resume.Id, offer.Id, "local", false));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var analysis = await response.ReadAsync<ResumeAnalysisDto>();

        // Required: C# + PostgreSQL found, Java missing (JavaScript would not count); preferred: Docker found.
        Assert.Equal((int)Math.Round(100.0 * (2 + 2 + 1) / (2 * 3 + 2)), analysis.Summary.MatchScore);
        Assert.Equal(["C#", "PostgreSQL", "Docker"], analysis.Result.MatchedSkills.Select(s => s.Skill));
        Assert.All(analysis.Result.MatchedSkills, s => Assert.NotNull(s.ResumeEvidence));
        Assert.Contains(analysis.Result.MissingSkills, s => s.Skill == "Java" && s.Required);
        Assert.Contains(analysis.Result.MissingSkills, s => s.Skill == "Kubernetes" && !s.Required);
        Assert.Equal("local", analysis.Summary.Provider);
        Assert.Equal(offer.Title, analysis.Summary.JobTitle);

        var history = await (await user.Client.GetAsync($"/api/v1/resume-analyses?resumeId={resume.Id}"))
            .ReadAsync<List<ResumeAnalysisSummaryDto>>();
        Assert.Equal(analysis.Summary.Id, Assert.Single(history).Id);

        // Deleting the resume deletes its analyses too.
        await (await user.Client.DeleteAsync($"/api/v1/resumes/{resume.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        await (await user.Client.GetAsync($"/api/v1/resume-analyses/{analysis.Summary.Id}")).EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Ai_provider_without_api_key_is_reported_unavailable_and_the_app_keeps_working()
    {
        var user = await factory.RegisterUserAsync();
        var providers = await (await user.Client.GetAsync("/api/v1/resume-analyses/providers")).ReadAsync<List<AnalysisProviderDto>>();
        Assert.Contains(providers, p => p is { Id: "local", Available: true, External: false });
        Assert.Contains(providers, p => p is { Id: "anthropic", Available: false, External: true });

        var resume = await (await user.Client.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(ResumeFixtures.Docx(), "cv.docx"))).ReadAsync<ResumeDto>();
        var offer = await user.Client.CreateOfferAsync();
        var response = await user.Client.PostJsonAsync("/api/v1/resume-analyses",
            new CreateResumeAnalysisRequest(resume.Id, offer.Id, "anthropic", true));

        await response.EnsureStatusAsync(HttpStatusCode.ServiceUnavailable);
        Assert.Equal("ai.not_configured", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task External_providers_require_consent_and_their_output_is_sanitized()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IResumeAnalysisService, FakeExternalAnalyzer>()));
        var user = await RegisterOn(app);
        var resume = await (await user.PostAsync("/api/v1/resumes",
            ResumeFixtures.Upload(ResumeFixtures.Docx(), "cv.docx"))).ReadAsync<ResumeDto>();
        var offer = await user.CreateOfferAsync();

        var withoutConsent = await user.PostJsonAsync("/api/v1/resume-analyses",
            new CreateResumeAnalysisRequest(resume.Id, offer.Id, FakeExternalAnalyzer.ProviderKey, false));
        await withoutConsent.EnsureStatusAsync(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("analysis.consent_required", await withoutConsent.ProblemCodeAsync());

        var withConsent = await user.PostJsonAsync("/api/v1/resume-analyses",
            new CreateResumeAnalysisRequest(resume.Id, offer.Id, FakeExternalAnalyzer.ProviderKey, true));
        await withConsent.EnsureStatusAsync(HttpStatusCode.Created);
        var analysis = await withConsent.ReadAsync<ResumeAnalysisDto>();
        var result = analysis.Result;

        Assert.Equal(100, result.MatchScore); // clamped from 250
        var csharp = Assert.Single(result.MatchedSkills); // "Rust" had no support in the CV and was dropped
        Assert.Equal("C#", csharp.Skill);
        Assert.NotNull(csharp.ResumeEvidence);
        Assert.Null(result.RelevantExperience.Single().ResumeEvidence); // invented quote removed
        Assert.Equal(FakeExternalAnalyzer.ModelId, analysis.Summary.Model);
    }

    private static async Task<HttpClient> RegisterOn(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostJsonAsync("/api/v1/auth/register",
            new Application.Auth.RegisterRequest($"fake-{Guid.NewGuid():N}@test.local", "Sup3rSecret", "Fake", "UTC"));
        await response.EnsureStatusAsync(HttpStatusCode.Created);
        var auth = await response.ReadAsync<Application.Auth.AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private sealed class FakeExternalAnalyzer : IResumeAnalysisService
    {
        public const string ProviderKey = "fake-external";
        public const string ModelId = "fake-model-1";

        public string ProviderId => ProviderKey;
        public string DisplayName => "Fake external";
        public bool IsExternal => true;
        public bool IsAvailable => true;
        public string? ModelName => ModelId;

        public Task<ResumeAnalysisOutcome> AnalyzeAsync(ResumeAnalysisInput input, CancellationToken ct) =>
            Task.FromResult(new ResumeAnalysisOutcome(
                new ResumeAnalysisResult(
                    250,
                    "Resumen",
                    [
                        new SkillMatch("C#", "APIs con C# y ASP.NET Core", null),
                        new SkillMatch("Rust", "10 años de Rust en sistemas embebidos", null),
                    ],
                    [],
                    [new ExperienceEvidence("Lideró un equipo de 20 personas", "Director de ingeniería en Google")],
                    [],
                    ["Fortaleza"],
                    [],
                    ["Recomendación"]),
                ModelId,
                "test"));
    }
}
