using LaunchPad.Application.Common;
using LaunchPad.Application.ProjectSupport;
using LaunchPad.Application.SharePoint;
using LaunchPad.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LaunchPad.Api.Controllers;

[ApiController]
[Route("api/project-support")]
[Authorize(Roles = $"{Roles.SubjectMatterExpert},{Roles.Candidate},{Roles.ProgramOps}")]
public class ProjectSupportController(
    IProjectSupportRepository support, ICurrentUser user, IAppUserRepository users,
    IDocumentStorage documents, IFolderProvisioner folders, IAuditLog audit) : ControllerBase
{
    [HttpGet("projects")]
    public async Task<IActionResult> Projects(CancellationToken ct) => Ok(await support.ProjectsAsync(ct));

    [HttpGet("experts")]
    [Authorize(Roles = Roles.ProgramOps)]
    public async Task<IActionResult> Experts(CancellationToken ct) => Ok(await support.ExpertsAsync(null, ct));

    [HttpGet("projects/{projectId:int}/experts")]
    public async Task<IActionResult> ProjectExperts(int projectId, CancellationToken ct)
    {
        if (!await support.CanAccessAsync(projectId, ct)) return Forbid();
        return Ok(await support.ExpertsAsync(projectId, ct));
    }

    [HttpPut("projects/{projectId:int}/experts/{userId:int}")]
    [Authorize(Roles = Roles.ProgramOps)]
    public Task<IActionResult> Assign(int projectId, int userId, CancellationToken ct) => SetExpert(projectId, userId, true, ct);

    [HttpDelete("projects/{projectId:int}/experts/{userId:int}")]
    [Authorize(Roles = Roles.ProgramOps)]
    public Task<IActionResult> Unassign(int projectId, int userId, CancellationToken ct) => SetExpert(projectId, userId, false, ct);

    private async Task<IActionResult> SetExpert(int projectId, int userId, bool assigned, CancellationToken ct)
    {
        if (!await support.SetExpertAsync(projectId, userId, assigned, ct)) return NotFound();
        await audit.RecordAsync(user.EntraObjectId, "Project", projectId.ToString(), assigned ? "ExpertAssigned" : "ExpertUnassigned", data: new { AppUserId = userId }, ct: ct);
        return NoContent();
    }

    [HttpGet("projects/{projectId:int}/questions")]
    public async Task<IActionResult> Questions(int projectId, CancellationToken ct)
    {
        if (!await support.CanAccessAsync(projectId, ct)) return Forbid();
        return Ok(await support.QuestionsAsync(projectId, ct));
    }

    [HttpPost("projects/{projectId:int}/questions")]
    [EnableRateLimiting(RateLimitPolicies.CommunityWrite)]
    public async Task<IActionResult> Ask(int projectId, CreateQuestionRequest request, CancellationToken ct)
    {
        if (!await support.CanAccessAsync(projectId, ct)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 10000)
            return BadRequest("A title (up to 200 characters) and message (up to 10,000 characters) are required.");
        if (request.DeliverableId is int deliverableId)
        {
            if (!await support.IsExpertAsync(projectId, ct)) return Forbid();
            if (await support.DeliverableAsync(projectId, deliverableId, ct) is null) return NotFound();
        }
        else if (!await support.IsCandidateAsync(projectId, ct)) return Forbid();
        var authorId = await users.GetIdByEntraObjectIdAsync(user.EntraObjectId, ct);
        if (authorId is null) return Forbid();
        var question = new ProjectQuestion
        {
            ProjectId = projectId,
            AuthorAppUserId = authorId.Value,
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            DeliverableId = request.DeliverableId
        };
        await support.AddQuestionAsync(question, ct);
        return Ok(new { question.ProjectQuestionId });
    }

    [HttpPost("projects/{projectId:int}/questions/{questionId:int}/answers")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [EnableRateLimiting(RateLimitPolicies.CommunityWrite)]
    public async Task<IActionResult> Answer(int projectId, int questionId, [FromForm] string? body, IFormFile? file, CancellationToken ct)
    {
        if (!await support.IsExpertAsync(projectId, ct) && !await support.IsCandidateAsync(projectId, ct)) return Forbid();
        if (await support.QuestionAsync(projectId, questionId, ct) is null) return NotFound();
        if ((string.IsNullOrWhiteSpace(body) && file is null) || body?.Length > 10000)
            return BadRequest("Add a message (up to 10,000 characters) or an attachment.");
        var authorId = await users.GetIdByEntraObjectIdAsync(user.EntraObjectId, ct);
        if (authorId is null) return Forbid();
        var answer = new ProjectAnswer { ProjectQuestionId = questionId, AuthorAppUserId = authorId.Value, Body = body?.Trim() ?? "" };
        if (file is not null)
        {
            var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
            if (file.Length <= 0 || file.Length > 10 * 1024 * 1024 || fileName.Length > 200 || !FileSignatures.IsAllowedDeliverableFileName(fileName))
                return BadRequest("Choose a supported document or image up to 10 MB with a filename up to 200 characters.");
            await using var stream = file.OpenReadStream();
            var header = new byte[FileSignatures.HeaderBytes];
            var count = await stream.ReadAtLeastAsync(header, header.Length, false, ct);
            if (!FileSignatures.MatchesExtension(header.AsSpan(0, count), Path.GetExtension(fileName)))
                return BadRequest("The file contents do not match the filename extension.");
            stream.Position = 0;
            var project = await support.ProjectAsync(projectId, ct);
            if (project is null) return NotFound();
            if (project.SharePointFolderId is null)
            {
                var folder = await folders.EnsureProjectFolderAsync(project.Cohort.Name, project.Name, ct);
                project.SharePointFolderId = folder.FolderId;
                project.SharePointFolderWebUrl = folder.WebUrl;
                await support.SaveAsync(ct);
            }
            answer.FileName = fileName;
            // Unique storage names prevent a later response overwriting an earlier attachment.
            answer.FileItemId = await documents.SaveAsync(project.SharePointFolderId, $"{Guid.NewGuid():N}-{fileName}", stream, "application/octet-stream", file.Length, ct);
        }
        try { await support.AddAnswerAsync(answer, ct); }
        catch
        {
            if (answer.FileItemId is not null) await documents.DeleteAsync(answer.FileItemId, CancellationToken.None);
            throw;
        }
        return Ok(new { answer.ProjectAnswerId });
    }

    [HttpGet("projects/{projectId:int}/questions/{questionId:int}/answers/{answerId:int}/file")]
    [EnableRateLimiting(RateLimitPolicies.Download)]
    public async Task<IActionResult> Attachment(int projectId, int questionId, int answerId, CancellationToken ct)
    {
        if (!await support.CanAccessAsync(projectId, ct)) return Forbid();
        var answer = await support.AnswerAsync(projectId, questionId, answerId, ct);
        if (answer?.FileItemId is null) return NotFound();
        var file = await documents.GetAsync(answer.FileItemId, ct);
        return file is null ? NotFound() : File(file.Value.Content, "application/octet-stream", answer.FileName);
    }

    [HttpGet("projects/{projectId:int}/deliverables")]
    public async Task<IActionResult> Deliverables(int projectId, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.ProgramOps) && !await support.IsExpertAsync(projectId, ct)) return Forbid();
        return Ok(await support.DeliverablesAsync(projectId, ct));
    }

    [HttpGet("projects/{projectId:int}/deliverables/{deliverableId:int}/file")]
    [EnableRateLimiting(RateLimitPolicies.Download)]
    public async Task<IActionResult> DeliverableFile(int projectId, int deliverableId, CancellationToken ct)
    {
        if (!user.IsInRole(Roles.ProgramOps) && !await support.IsExpertAsync(projectId, ct)) return Forbid();
        var deliverable = await support.DeliverableAsync(projectId, deliverableId, ct);
        if (deliverable?.SharePointItemId is null) return NotFound();
        var file = await documents.GetAsync(deliverable.SharePointItemId, ct);
        return file is null ? NotFound() : File(file.Value.Content, "application/octet-stream", deliverable.FileName);
    }
}
