using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.tag_id);
                });

            migrationBuilder.CreateTable(
                name: "answer_votes",
                columns: table => new
                {
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    value = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answer_votes", x => new { x.answer_id, x.voter_id });
                    table.CheckConstraint("ck_answer_votes_value", "value IN (-1, 1)");
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    answer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    author_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answers", x => x.answer_id);
                    table.UniqueConstraint("ak_answers_question_id_answer_id", x => new { x.question_id, x.answer_id });
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    body = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    author_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    author_display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    book_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_answer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.question_id);
                    table.ForeignKey(
                        name: "fk_questions_accepted_answer",
                        columns: x => new { x.question_id, x.accepted_answer_id },
                        principalTable: "answers",
                        principalColumns: new[] { "question_id", "answer_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "question_tags",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_tags", x => new { x.question_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_question_tags_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_question_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_votes",
                columns: table => new
                {
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voter_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    value = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_votes", x => new { x.question_id, x.voter_id });
                    table.CheckConstraint("ck_question_votes_value", "value IN (-1, 1)");
                    table.ForeignKey(
                        name: "fk_question_votes_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "question_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_question_tags_tag_id",
                table: "question_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_question_id_accepted_answer_id",
                table: "questions",
                columns: new[] { "question_id", "accepted_answer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_tags_slug",
                table: "tags",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_answer_votes_answers_answer_id",
                table: "answer_votes",
                column: "answer_id",
                principalTable: "answers",
                principalColumn: "answer_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_answers_questions_question_id",
                table: "answers",
                column: "question_id",
                principalTable: "questions",
                principalColumn: "question_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_questions_accepted_answer",
                table: "questions");

            migrationBuilder.DropTable(
                name: "answer_votes");

            migrationBuilder.DropTable(
                name: "question_tags");

            migrationBuilder.DropTable(
                name: "question_votes");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "questions");
        }
    }
}
