using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cortex.QuestionService.Data;

public sealed class QuestionDbContext : DbContext
{
    public DbSet<Answer> Answers { get; set; }
    public DbSet<AnswerVote> AnswerVotes { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<QuestionVote>  QuestionVotes { get; set; }
    public DbSet<QuestionViewCount> QuestionViewCounts { get; set; }
    public DbSet<Tag> Tags { get; set; }
    
    public QuestionDbContext(DbContextOptions<QuestionDbContext> options) : base(options)
    {
        // Wolverine's transactional middleware owns the transaction (Eager mode) and rolls the whole
        // thing back on any failure, so EF's automatic savepoints around each SaveChanges protect
        // nothing; they only add a SAVEPOINT / RELEASE pair of round trips per save.
        Database.AutoSavepointsEnabled = false;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QuestionDbContext).Assembly);
}