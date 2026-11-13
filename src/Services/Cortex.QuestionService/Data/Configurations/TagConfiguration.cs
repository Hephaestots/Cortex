using Cortex.QuestionService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cortex.QuestionService.Data.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        
        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Id)
            .HasColumnName("tag_id");

        builder.Property(tag => tag.Name)
            .HasMaxLength(Tag.MaxNameLength);

        builder.Property(tag => tag.Slug)
            .HasMaxLength(Tag.MaxSlugLength);

        builder.Property(tag => tag.Description)
            .HasMaxLength(Tag.MaxDescriptionLength);

        // The slug is the tag's public identifier (URLs, lookups), so it must be unique.
        builder.HasIndex(tag => tag.Slug)
            .IsUnique();
    }
}