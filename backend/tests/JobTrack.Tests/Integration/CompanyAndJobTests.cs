using System.Net;
using JobTrack.Application.Common;
using JobTrack.Application.Companies;
using JobTrack.Application.JobOffers;
using JobTrack.Domain.Enums;

namespace JobTrack.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class CompanyAndJobTests(ApiFactory factory)
{
    [Fact]
    public async Task Company_crud_round_trip()
    {
        var user = await factory.RegisterUserAsync();
        var created = await user.Client.CreateCompanyAsync("Acme");
        Assert.Equal("Acme", created.Name);
        Assert.Equal(0, created.JobOfferCount);

        var update = await user.Client.PutJsonAsync($"/api/v1/companies/{created.Id}", new SaveCompanyRequest(
            "Acme Corp", "https://acme.example", "Retail", "Lima", "Desc", "Ana", "ana@acme.example", "+51 1 234", "  "));
        await update.EnsureStatusAsync(HttpStatusCode.OK);
        var updated = await update.ReadAsync<CompanyDto>();
        Assert.Equal("Acme Corp", updated.Name);
        Assert.Equal("ana@acme.example", updated.ContactEmail);
        Assert.Null(updated.Notes); // blank strings are stored as null

        var page = await (await user.Client.GetAsync("/api/v1/companies?search=acme")).ReadAsync<PagedResult<CompanyDto>>();
        Assert.Single(page.Items);

        await (await user.Client.DeleteAsync($"/api/v1/companies/{created.Id}")).EnsureStatusAsync(HttpStatusCode.NoContent);
        await (await user.Client.GetAsync($"/api/v1/companies/{created.Id}")).EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Company_names_are_unique_per_user_ignoring_case_but_not_across_users()
    {
        var user = await factory.RegisterUserAsync();
        await user.Client.CreateCompanyAsync("Globex");

        var duplicate = await user.Client.PostJsonAsync("/api/v1/companies",
            new SaveCompanyRequest("  GLOBEX ", null, null, null, null, null, null, null, null));
        await duplicate.EnsureStatusAsync(HttpStatusCode.Conflict);
        Assert.Equal("company.duplicate_name", await duplicate.ProblemCodeAsync());

        var other = await factory.RegisterUserAsync();
        await other.Client.CreateCompanyAsync("Globex");
    }

    [Fact]
    public async Task Company_validation_rejects_bad_email_and_url()
    {
        var user = await factory.RegisterUserAsync();
        var response = await user.Client.PostJsonAsync("/api/v1/companies",
            new SaveCompanyRequest("X", "javascript:alert(1)", null, null, null, null, "not-an-email", null, null));

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("website", out _));
        Assert.True(errors.TryGetProperty("contactEmail", out _));
    }

    [Fact]
    public async Task Company_with_offers_cannot_be_deleted()
    {
        var user = await factory.RegisterUserAsync();
        var offer = await user.Client.CreateOfferAsync();

        var response = await user.Client.DeleteAsync($"/api/v1/companies/{offer.CompanyId}");

        await response.EnsureStatusAsync(HttpStatusCode.Conflict);
        Assert.Equal("company.has_job_offers", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Offer_is_created_with_normalized_skills_and_currency()
    {
        var user = await factory.RegisterUserAsync();
        var offer = await user.Client.CreateOfferAsync();

        Assert.Equal(["C#", ".NET"], offer.RequiredSkills);
        Assert.Equal("EUR", offer.Currency);
        Assert.Null(offer.Application);
        Assert.Equal(1, (await (await user.Client.GetAsync($"/api/v1/companies/{offer.CompanyId}")).ReadAsync<CompanyDto>()).JobOfferCount);
    }

    [Fact]
    public async Task Offer_validation_rejects_inverted_salary_and_missing_currency()
    {
        var user = await factory.RegisterUserAsync();
        var company = await user.Client.CreateCompanyAsync();
        var request = TestHelpers.OfferRequest(company.Id) with { SalaryMin = 50_000, SalaryMax = 10_000, Currency = null };

        var response = await user.Client.PostJsonAsync("/api/v1/jobs", request);

        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("salaryMax", out _));
        Assert.True(errors.TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task Offers_can_be_filtered_by_application_state_and_sorted()
    {
        var user = await factory.RegisterUserAsync();
        var company = await user.Client.CreateCompanyAsync();
        var applied = await user.Client.CreateOfferAsync(company.Id);
        var saved = await user.Client.CreateOfferAsync(company.Id);
        await user.Client.CreateApplicationAsync(applied.Id, ApplicationStatus.Applied);

        var notApplied = await (await user.Client.GetAsync("/api/v1/jobs?hasApplication=false")).ReadAsync<PagedResult<JobOfferListItemDto>>();
        var withApplication = await (await user.Client.GetAsync("/api/v1/jobs?hasApplication=true")).ReadAsync<PagedResult<JobOfferListItemDto>>();

        Assert.Equal(saved.Id, Assert.Single(notApplied.Items).Id);
        var item = Assert.Single(withApplication.Items);
        Assert.Equal(applied.Id, item.Id);
        Assert.Equal(ApplicationStatus.Applied, item.Application!.Status);

        var invalidSort = await user.Client.GetAsync("/api/v1/jobs?sort=salary");
        await invalidSort.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Paging_is_validated_and_applied()
    {
        var user = await factory.RegisterUserAsync();
        for (var i = 0; i < 3; i++)
        {
            await user.Client.CreateCompanyAsync($"Company {i}");
        }

        var page = await (await user.Client.GetAsync("/api/v1/companies?page=2&pageSize=2")).ReadAsync<PagedResult<CompanyDto>>();
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal("Company 2", Assert.Single(page.Items).Name);

        await (await user.Client.GetAsync("/api/v1/companies?pageSize=1000")).EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Offer_with_application_cannot_be_deleted()
    {
        var user = await factory.RegisterUserAsync();
        var application = await user.Client.CreateApplicationAsync();

        var response = await user.Client.DeleteAsync($"/api/v1/jobs/{application.Summary.JobOfferId}");

        await response.EnsureStatusAsync(HttpStatusCode.Conflict);
        Assert.Equal("job.has_application", await response.ProblemCodeAsync());
    }
}
