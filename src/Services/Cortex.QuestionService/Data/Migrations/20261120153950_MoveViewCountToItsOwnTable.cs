using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveViewCountToItsOwnTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-ordered: EF generated the DropColumn first, which would lose every count, and
            // no row for existing questions, which would make their GET a 404 (the view counter
            // UPDATE finds nothing). Create, copy, then drop.
            migrationBuilder.CreateTable(
                name: "question_view_counts",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_view_counts", x => x.question_id);
                    table.ForeignKey(
                        name: "fk_question_view_counts_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                });

            // One counter row per existing question, keeping its count.
            migrationBuilder.Sql(
                "INSERT INTO question_view_counts (question_id, count) SELECT question_id, view_count FROM questions;");

            migrationBuilder.DropColumn(
                name: "view_count",
                table: "questions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reverse order: add the column back, copy the counts, then drop the table.
            migrationBuilder.AddColumn<int>(
                name: "view_count",
                table: "questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE questions SET view_count = c.count FROM question_view_counts c WHERE c.question_id = questions.question_id;");

            migrationBuilder.DropTable(
                name: "question_view_counts");
        }
    }
}
