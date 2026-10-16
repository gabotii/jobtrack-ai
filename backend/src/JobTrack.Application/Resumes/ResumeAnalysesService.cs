using System.Linq.Expressions;
using JobTrack.Application.Abstractions;
using JobTrack.Application.Common;
using JobTrack.Domain.Common;
using JobTrack.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobTrack.Application.Resumes;

/// <summary>
/// Runs a CV-vs-offer analysis with the chosen provider, sanitizes the result and keeps it in the history.
/// External providers require explicit consent on every request; unavailable providers fail without affecting the
/// rest of the application.
/// </summary>
public sealed class ResumeAnalysesService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IEnumerable<IResumeAnalysisService> analyzers,
    ILogger<ResumeAnalysesService> logger)
{
    private static readonly Expression<Func<ResumeAnalysis, ResumeAnalysisSummaryDto>> ToSummary = a =>
        new ResumeAnalysisSummaryDto(
            a.Id,
            a.ResumeId,
            a.Resume!.FileName,
            a.JobOfferId,
            a.JobTitleSnapshot,
            a.CompanyNameSnapshot,
            a.Provider,
            a.Model,
            a.MatchScore,
            a.CreatedAt);

    public IReadOnlyList<AnalysisProviderDto> GetProviders() =>
        analyzers
            .OrderBy(a => a.IsExternal)
            .Select(a => new AnalysisProviderDto(
                a.ProviderId,
                a.DisplayName,
                a.IsAvailable,
                a.IsExternal,
                a.ModelName,
                a.IsExternal
                    ? "Envía el texto de tu CV y de la oferta al proveedor de IA para un análisis semántico."
                    : "Se ejecuta en el servidor de JobTrack; ningún dato sale de la aplicación."))
            .ToList();

    public async Task<IReadOnlyList<ResumeAnalysisSummaryDto>> ListAsync(ResumeAnalysisQuery query, CancellationToken ct)
    {
        var analyses = db.ResumeAnalyses.AsNoTracking();
        if (query.ResumeId is { } resumeId)
        {
            analyses = analyses.Where(a => a.ResumeId == resumeId);
        }

        if (query.JobOfferId is { } jobOfferId)
        {
            analyses = analyses.Where(a => a.JobOfferId == jobOfferId);
        }

        return await analyses.OrderByDescending(a => a.CreatedAt).Take(100).Select(ToSummary).ToListAsync(ct);
    }

    public async Task<ResumeAnalysisDto> GetAsync(Guid id, CancellationToken ct)
    {
        var summary = await db.ResumeAnalyses.AsNoTracking().Where(a => a.Id == id).Select(ToSummary).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Análisis");
        var details = await db.ResumeAnalyses.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.PromptVersion, a.Result })
            .SingleAsync(ct);

        return new ResumeAnalysisDto(summary, details.PromptVersion, details.Result);
    }

    public async Task<ResumeAnalysisDto> CreateAsync(CreateResumeAnalysisRequest request, CancellationToken ct)
    {
        var analyzer = analyzers.FirstOrDefault(a => a.ProviderId == request.Provider)
            ?? throw new DomainException("analysis.unknown_provider", "El proveedor de análisis indicado no existe.");

        if (!analyzer.IsAvailable)
        {
            throw new ExternalServiceException(
                "ai.not_configured",
                "El análisis con IA no está configurado en este servidor. Usa el análisis básico.",
                503);
        }

        if (analyzer.IsExternal && !request.ConsentToExternalProcessing)
        {
            throw new DomainException(
                "analysis.consent_required",
                "Debes aceptar el envío del texto de tu CV al proveedor de IA para usar este análisis.");
        }

        var resume = await db.Resumes.AsNoTracking()
            .Where(r => r.Id == request.ResumeId)
            .Select(r => new { r.Id, r.ExtractedText })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("CV");

        var offer = await db.JobOffers.AsNoTracking()
            .Where(o => o.Id == request.JobOfferId)
            .Select(o => new
            {
                o.Id,
                o.Title,
                CompanyName = o.Company!.Name,
                o.Description,
                o.RequiredSkills,
                o.PreferredSkills,
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Oferta");

        if (string.IsNullOrWhiteSpace(offer.Description) && offer.RequiredSkills.Count == 0 && offer.PreferredSkills.Count == 0)
        {
            throw new DomainException(
                "analysis.offer_without_content",
                "La oferta no tiene descripción ni habilidades. Añádelas para poder compararla con tu CV.");
        }

        var input = new ResumeAnalysisInput(
            resume.ExtractedText,
            offer.Title,
            offer.CompanyName,
            offer.Description,
            offer.RequiredSkills,
            offer.PreferredSkills);

        var outcome = await analyzer.AnalyzeAsync(input, ct);
        var result = ResumeAnalysisResultSanitizer.Sanitize(outcome.Result, input);

        var analysis = new ResumeAnalysis(
            currentUser.RequiredUserId,
            resume.Id,
            offer.Id,
            offer.Title,
            offer.CompanyName,
            analyzer.ProviderId,
            outcome.Model,
            outcome.PromptVersion,
            result);
        db.ResumeAnalyses.Add(analysis);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Resume analysis {AnalysisId} created with {Provider}/{Model}, score {Score}",
            analysis.Id, analyzer.ProviderId, outcome.Model, result.MatchScore);
        return await GetAsync(analysis.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var analysis = await db.ResumeAnalyses.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException("Análisis");

        db.ResumeAnalyses.Remove(analysis);
        await db.SaveChangesAsync(ct);
    }
}
