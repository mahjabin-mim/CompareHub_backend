using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompareHub.Backend.app.Core.Domain.Entities;

namespace CompareHub.Backend.app.Core.Infrastructure.Persistence.Configurations;

public class UserSourceLinkConfiguration : IEntityTypeConfiguration<UserSourceLink>
{
    public void Configure(EntityTypeBuilder<UserSourceLink> builder)
    {
        builder.ToTable("user_source_links");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Url).HasColumnName("url").HasMaxLength(1024).IsRequired();
        builder.Property(x => x.WebsiteName).HasColumnName("website_name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.SourceLinks)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
