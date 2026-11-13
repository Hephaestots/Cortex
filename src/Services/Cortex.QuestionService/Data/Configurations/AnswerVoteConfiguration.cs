using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class AnswerVoteConfiguration : IEntityTypeConfiguration<AnswerVote>
{
    public void Configure(EntityTypeBuilder<AnswerVote> builder)
    {
        builder.ToTable("answer_votes", table =>
            table.HasCheckConstraint("ck_answer_votes_value", "value IN (-1, 1)"));

        builder.HasKey(vote => new { vote.AnswerId, vote.VoterId });

        builder.Property(vote => vote.VoterId)
            .HasMaxLength(UserIdRules.MaxLength);

        builder.HasOne(vote => vote.Answer)
            .WithMany(answer => answer.Votes)
            .HasForeignKey(vote => vote.AnswerId);
    }
}