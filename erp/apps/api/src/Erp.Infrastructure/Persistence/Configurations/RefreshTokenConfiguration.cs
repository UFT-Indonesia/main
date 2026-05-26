using Erp.Core.Aggregates.Auth;
using Erp.Infrastructure.Identity;
using Erp.Infrastructure.Persistence.ValueConverters;
using Erp.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

namespace Erp.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    private static readonly ValueConverter<Instant, DateTimeOffset> InstantConverter = new(
        instant => instant.ToDateTimeOffset(),
        dateTimeOffset => Instant.FromDateTimeOffset(dateTimeOffset));

    private static readonly ValueConverter<Instant?, DateTimeOffset?> NullableInstantConverter = new(
        instant => instant.HasValue ? instant.Value.ToDateTimeOffset() : null,
        dateTimeOffset => dateTimeOffset.HasValue ? Instant.FromDateTimeOffset(dateTimeOffset.Value) : null);

    private static readonly ValueConverter<RefreshTokenId?, Guid?> NullableRefreshTokenIdConverter = new(
        id => id.HasValue ? id.Value.Value : null,
        value => value.HasValue ? new RefreshTokenId(value.Value) : null);

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("AuthRefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasConversion(new RefreshTokenIdConverter());

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(token => token.FamilyId)
            .HasColumnName("family_id")
            .IsRequired();

        builder.Property(token => token.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.ExpiresAtUtc)
            .HasColumnName("expires_at_utc")
            .HasConversion(InstantConverter)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(token => token.CreatedByIp)
            .HasColumnName("created_by_ip")
            .HasMaxLength(64);

        builder.Property(token => token.CreatedByUserAgent)
            .HasColumnName("created_by_user_agent")
            .HasMaxLength(512);

        builder.Property(token => token.RevokedAtUtc)
            .HasColumnName("revoked_at_utc")
            .HasConversion(NullableInstantConverter)
            .HasColumnType("timestamp with time zone");

        builder.Property(token => token.RevokedReason)
            .HasColumnName("revoked_reason")
            .HasMaxLength(128);

        builder.Property(token => token.ReplacedByTokenId)
            .HasColumnName("replaced_by_token_id")
            .HasConversion(NullableRefreshTokenIdConverter);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => token.UserId);
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.ExpiresAtUtc);
    }
}
