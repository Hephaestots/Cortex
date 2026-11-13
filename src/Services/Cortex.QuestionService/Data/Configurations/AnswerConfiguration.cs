using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("answers");

        builder.HasKey(answer => answer.Id);

        // Unique (question_id, answer_id): the target of the composite FK
        // behind Question.AcceptedAnswerId (see "Accepted answer" in architecture.md).
        builder.HasAlternateKey(answer => new { answer.QuestionId, answer.Id })
            .HasName("ak_answers_question_id_answer_id");

        builder.Property(answer => answer.Id)
            .HasColumnName("answer_id");

        builder.Property(answer => answer.AuthorId)
            .HasMaxLength(UserIdRules.MaxLength);

        builder.Property(answer => answer.Body)
            .HasMaxLength(Answer.MaxBodyLength);

        builder.HasOne(answer => answer.Question)
            .WithMany(question => question.Answers)
            .HasForeignKey(answer => answer.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}