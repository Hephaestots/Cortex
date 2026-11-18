using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cortex.QuestionService.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. Question.Version maps to Postgres's xmin system column, which
            // every table already has. EF generated AddColumn("xmin"), which Postgres rejects
            // ("column name conflicts with a system column name"); Npgsql only skips it when the
            // table is created in the same migration. This migration exists to update the model
            // snapshot, so later migrations do not try to add the column again.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty, for the same reason as Up: xmin is a system column.
        }
    }
}
