using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CompareHub.Backend.app.Core.Domain.Entities;

namespace CompareHub.Backend.app.Core.Infrastructure.Persistence.Configurations;

public class UserProductSourceConfiguration : IEntityTypeConfiguration<UserProductSource>
{
    public void Configure(EntityTypeBuilder<UserProductSource> builder)
    {
        builder.ToTable("user_product_sources");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.SourceName).HasColumnName("source_name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SourceType).HasColumnName("source_type").HasMaxLength(50).IsRequired();
        builder.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(1024).IsRequired();
        builder.Property(x => x.SearchEndpoint).HasColumnName("search_endpoint").HasMaxLength(512).IsRequired();
        builder.Property(x => x.QueryParamName).HasColumnName("query_param_name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(10).IsRequired();
        builder.Property(x => x.ApiKeyEncrypted).HasColumnName("api_key_encrypted");
        builder.Property(x => x.HeadersJson).HasColumnName("headers_json");
        builder.Property(x => x.NamePath).HasColumnName("name_path").HasMaxLength(512).IsRequired();
        builder.Property(x => x.PricePath).HasColumnName("price_path").HasMaxLength(512).IsRequired();
        builder.Property(x => x.ImagePath).HasColumnName("image_path").HasMaxLength(512).IsRequired();
        builder.Property(x => x.ProductUrlPath).HasColumnName("product_url_path").HasMaxLength(512).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(x => x.ProductSources)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.IsActive });
    }
}
