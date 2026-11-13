using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");

        builder.HasKey(question => question.Id);

        builder.Property(question => question.Id)
            .HasColumnName("question_id");

        builder.Property(question => question.AuthorId)
            .HasMaxLength(UserIdRules.MaxLength);

        builder.Property(question => question.Body)
            .HasMaxLength(Question.MaxBodyLength);
        
        builder.Property(question => question.Title)
            .HasMaxLength(Question.MaxTitleLength);
        
        builder.Property(question => question.AuthorDisplayName)
            .HasMaxLength(Question.MaxDisplayNameLength);

        builder.Property(question => question.BookId)
            .HasMaxLength(Question.MaxBookIdLength);

        // See "Accepted answer" in architecture.md. The composite FK
        // (question_id, accepted_answer_id) -> answers (question_id, answer_id)
        // guarantees the accepted answer belongs to this question.
        builder.HasOne<Answer>()
            .WithMany()
            .HasForeignKey(question => new { question.Id, question.AcceptedAnswerId })
            .HasPrincipalKey(answer => new { answer.QuestionId, answer.Id })
            .HasConstraintName("fk_questions_accepted_answer")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(question => question.Tags)
            .WithMany(tag => tag.Questions)
            .UsingEntity("question_tags",
                // Named here because the default would be the plural tags_id / questions_id.
                right => right.HasOne(typeof(Tag)).WithMany().HasForeignKey("TagId"),
                left => left.HasOne(typeof(Question)).WithMany().HasForeignKey("QuestionId"));
    }
}