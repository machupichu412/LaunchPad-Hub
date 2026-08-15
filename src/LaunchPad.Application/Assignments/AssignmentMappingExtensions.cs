using LaunchPad.Domain.Entities;
using LaunchPad.Domain.Enums;

namespace LaunchPad.Application.Assignments;

public static class AssignmentMappingExtensions
{
    public static MyAssignmentDto ToMyAssignmentDto(this Assignment assignment, int tasksTotal, int tasksComplete) => new()
    {
        AssignmentId = assignment.AssignmentId,
        Status = assignment.Status,
        StartDate = assignment.StartDate,
        EndDate = assignment.EndDate,
        MatchScore = assignment.MatchScore,
        MatchRationale = assignment.MatchRationale,
        ProjectId = assignment.ProjectId,
        ProjectName = assignment.Project.Name,
        ProjectDescription = assignment.Project.Description,
        ProjectSkills = assignment.Project.Skills.Select(s => s.Skill.Name).ToArray(),
        SponsorName = assignment.Project.Sponsor.AppUser.DisplayName,
        SponsorOrganization = assignment.Project.Sponsor.Organization,
        TasksTotal = tasksTotal,
        TasksComplete = tasksComplete,
    };

    public static ProjectTodoDto ToDto(this ProjectTodo todo) => new()
    {
        ProjectTodoId = todo.ProjectTodoId,
        Title = todo.Title,
        Status = todo.Status,
        Priority = todo.Priority,
        DueDate = todo.DueDate,
    };

    public static DeliverableDto ToDto(this Deliverable deliverable) => new()
    {
        DeliverableId = deliverable.DeliverableId,
        Title = deliverable.Title,
        FileName = deliverable.FileName,
        Status = deliverable.Status,
        SubmittedUtc = deliverable.SubmittedUtc,
        ProjectTodoId = deliverable.ProjectTodoId,
        ProjectTodoTitle = deliverable.ProjectTodo?.Title,
        HasFile = deliverable.SharePointItemId is not null,
    };

    // Strengths/GrowthAreas/RecommendConversion only — OverallScore is never mapped here.
    public static CandidateEvaluationDto ToCandidateEvaluationDto(this Review review) => new()
    {
        ReviewId = review.ReviewId,
        Checkpoint = review.Checkpoint,
        SubmittedUtc = review.SubmittedUtc,
        Strengths = review.Strengths,
        GrowthAreas = review.GrowthAreas,
        RecommendConversion = review.RecommendConversion,
    };
}
