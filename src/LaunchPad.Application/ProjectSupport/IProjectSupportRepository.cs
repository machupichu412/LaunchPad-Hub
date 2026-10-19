using LaunchPad.Domain.Entities;
namespace LaunchPad.Application.ProjectSupport;

public record SupportProjectDto(int ProjectId, string Name, string? Description);
public record ExpertDto(int AppUserId, string DisplayName);
public record AnswerDto(int ProjectAnswerId, string AuthorName, string Body, DateTime CreatedUtc, string? FileName);
public record QuestionDto(int ProjectQuestionId, string Title, string Body, string AuthorName, DateTime CreatedUtc, int? DeliverableId, IReadOnlyList<AnswerDto> Answers);
public record SupportDeliverableDto(int DeliverableId, string Title, string FileName, string CandidateName, DateTime SubmittedUtc);
public record CreateQuestionRequest(string Title, string Body, int? DeliverableId = null);

public interface IProjectSupportRepository
{
    Task<IReadOnlyList<SupportProjectDto>> ProjectsAsync(CancellationToken ct);
    Task<bool> CanAccessAsync(int projectId, CancellationToken ct);
    Task<bool> IsExpertAsync(int projectId, CancellationToken ct);
    Task<bool> IsCandidateAsync(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ExpertDto>> ExpertsAsync(int? projectId, CancellationToken ct);
    Task<bool> SetExpertAsync(int projectId, int userId, bool assigned, CancellationToken ct);
    Task<IReadOnlyList<QuestionDto>> QuestionsAsync(int projectId, CancellationToken ct);
    Task<ProjectQuestion?> QuestionAsync(int projectId, int questionId, CancellationToken ct);
    Task AddQuestionAsync(ProjectQuestion question, CancellationToken ct);
    Task AddAnswerAsync(ProjectAnswer answer, CancellationToken ct);
    Task<ProjectAnswer?> AnswerAsync(int projectId, int questionId, int answerId, CancellationToken ct);
    Task<IReadOnlyList<SupportDeliverableDto>> DeliverablesAsync(int projectId, CancellationToken ct);
    Task<Deliverable?> DeliverableAsync(int projectId, int deliverableId, CancellationToken ct);
    Task<Project?> ProjectAsync(int projectId, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
