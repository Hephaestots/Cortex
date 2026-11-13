using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class QuestionVoteConfiguration : IEntityTypeConfiguration<QuestionVote>
{
    public void Configure(EntityTypeBuilder<QuestionVote> builder)
    {
        builder.ToTable("question_votes", table =>
            table.HasCheckConstraint("ck_question_votes_value", "value IN (-1, 1)"));

        builder.HasKey(vote => new { vote.QuestionId, vote.VoterId });

        builder.Property(vote => vote.VoterId)
            .HasMaxLength(UserIdRules.MaxLength);

        builder.HasOne(vote => vote.Question)
            .WithMany(question => question.Votes)
            .HasForeignKey(vote => vote.QuestionId);
    }
}