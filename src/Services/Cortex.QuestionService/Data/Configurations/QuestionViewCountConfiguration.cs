using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class QuestionViewCountConfiguration : IEntityTypeConfiguration<QuestionViewCount>
{
    public void Configure(EntityTypeBuilder<QuestionViewCount> builder)
    {
        builder.ToTable("question_view_counts");

        // One row per question: the question id is both the key and the foreign key.
        builder.HasKey(viewCount => viewCount.QuestionId);

        // Required one-to-one, deleted with the question by Postgres (DeleteQuestion loads only
        // the question row and relies on the database cascades).
        builder.HasOne<Question>()
            .WithOne(question => question.ViewCount)
            .HasForeignKey<QuestionViewCount>(viewCount => viewCount.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
