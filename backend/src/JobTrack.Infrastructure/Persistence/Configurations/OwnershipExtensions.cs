using JobTrack.Domain.Common;
using JobTrack.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTrack.Infrastructure.Persistence.Configurations;

internal static class OwnershipExtensions
{
    /// <summary>
    /// Common mapping for user-owned entities: client-generated UUIDv7 key, FK to the user (deleting the account
    /// deletes all of its data) and timestamps.
    /// </summary>
    public static void ConfigureOwned<T>(this EntityTypeBuilder<T> builder)
        where T : OwnedEntity
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();
    }
}
