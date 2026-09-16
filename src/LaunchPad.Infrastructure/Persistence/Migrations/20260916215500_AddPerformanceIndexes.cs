using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LaunchPad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Project_CohortId",
                table: "Project");

            migrationBuilder.DropIndex(
                name: "IX_Deliverable_AssignmentId",
                table: "Deliverable");

            migrationBuilder.DropIndex(
                name: "IX_Candidate_CohortId",
                table: "Candidate");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_ProjectId",
                table: "Assignment");

            migrationBuilder.CreateIndex(
                name: "IX_Review_Type_Checkpoint_Submitted",
                table: "Review",
                columns: new[] { "ReviewType", "Checkpoint", "SubmittedUtc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Project_Cohort_Status_Approval",
                table: "Project",
                columns: new[] { "CohortId", "Status", "ApprovalStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_Recipient_Created",
                table: "Notification",
                columns: new[] { "RecipientAppUserId", "CreatedUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Deliverable_Assignment_Submitted",
                table: "Deliverable",
                columns: new[] { "AssignmentId", "SubmittedUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Candidate_Cohort_Status",
                table: "Candidate",
                columns: new[] { "CohortId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Candidate_Status",
                table: "Assignment",
                columns: new[] { "CandidateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Project_Status",
                table: "Assignment",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_Status",
                table: "Assignment",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Review_Type_Checkpoint_Submitted",
                table: "Review");

            migrationBuilder.DropIndex(
                name: "IX_Project_Cohort_Status_Approval",
                table: "Project");

            migrationBuilder.DropIndex(
                name: "IX_Notification_Recipient_Created",
                table: "Notification");

            migrationBuilder.DropIndex(
                name: "IX_Deliverable_Assignment_Submitted",
                table: "Deliverable");

            migrationBuilder.DropIndex(
                name: "IX_Candidate_Cohort_Status",
                table: "Candidate");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_Candidate_Status",
                table: "Assignment");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_Project_Status",
                table: "Assignment");

            migrationBuilder.DropIndex(
                name: "IX_Assignment_Status",
                table: "Assignment");

            migrationBuilder.CreateIndex(
                name: "IX_Project_CohortId",
                table: "Project",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverable_AssignmentId",
                table: "Deliverable",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Candidate_CohortId",
                table: "Candidate",
                column: "CohortId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignment_ProjectId",
                table: "Assignment",
                column: "ProjectId");
        }
    }
}
