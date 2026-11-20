using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnswerMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author_display_name",
                table: "answers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // No AddColumn for Answer.Version: it maps to Postgres's xmin system column, which
            // every table already has, and Postgres rejects adding it ("column name conflicts
            // with a system column name"). EF generated it anyway; see AddQuestionVersion.
            // The model snapshot still records the property, so later migrations won't add it.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "author_display_name",
                table: "answers");

            // No DropColumn for xmin, for the same reason as in Up: it is a system column.
        }
    }
}
