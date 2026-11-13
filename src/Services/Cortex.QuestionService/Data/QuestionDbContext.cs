using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Data;

public sealed class QuestionDbContext(DbContextOptions<QuestionDbContext> options) : DbContext(options)
{
    public DbSet<Answer> Answers { get; set; }
    public DbSet<AnswerVote> AnswerVotes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionVote>  QuestionVotes { get; set; }
    public DbSet<Tag> Tags { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionDbContext).Assembly);
}