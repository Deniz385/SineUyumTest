namespace SineUyum.Api.Data
{
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using SineUyum.Api.Models;

    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<AppUser>(options)
    {
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Genre> Genres { get; set; } // Yeni eklendi
        public DbSet<MovieGenre> MovieGenres { get; set; } // Yeni eklendi
        public DbSet<UserRating> UserRatings { get; set; }
        public DbSet<Watchlist> Watchlists { get; set; }
        public DbSet<WatchlistItem> WatchlistItems { get; set; }
        public DbSet<UserFollow> UserFollows { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<CinemaEvent> CinemaEvents { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }
        public DbSet<EventGroup> EventGroups { get; set; }
        public DbSet<EventGroupMember> EventGroupMembers { get; set; }
        public DbSet<EventGroupSuggestion> EventGroupSuggestions { get; set; }
        public DbSet<MatchingRun> MatchingRuns { get; set; }
        public DbSet<EventVote> EventVotes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<DeviceToken> DeviceTokens { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<UserBlock> UserBlocks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Bir FCM token'ı tek kayda karşılık gelir (upsert bununla yapılır);
            // kullanıcı silinince cihaz kayıtları da düşer.
            builder.Entity<DeviceToken>()
                .HasIndex(d => d.Token)
                .IsUnique();

            builder.Entity<DeviceToken>()
                .HasIndex(d => d.UserId);

            // Bir sağlayıcı satın alması tek bir kayda (ve tek bir hesaba) bağlanır.
            builder.Entity<Subscription>()
                .HasIndex(s => new { s.Provider, s.ProviderReference })
                .IsUnique();

            builder.Entity<Subscription>()
                .HasIndex(s => new { s.UserId, s.CurrentPeriodEnd });

            builder.Entity<Subscription>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Subscription>()
                .Property(s => s.Status)
                .HasConversion<string>()
                .HasMaxLength(16);

            builder.Entity<AppUser>()
                .Property(u => u.ProfileVisibility)
                .HasConversion<string>()
                .HasMaxLength(16);

            builder.Entity<AppUser>()
                .Property(u => u.MessagePermission)
                .HasConversion<string>()
                .HasMaxLength(16);

            // Bir çift için tek kayıt; "beni kim engelledi" sorgusu için ters indeks.
            builder.Entity<UserBlock>()
                .HasKey(b => new { b.BlockerId, b.BlockedId });

            builder.Entity<UserBlock>()
                .HasIndex(b => b.BlockedId);

            builder.Entity<UserBlock>()
                .HasOne(b => b.Blocker)
                .WithMany()
                .HasForeignKey(b => b.BlockerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<UserBlock>()
                .HasOne(b => b.Blocked)
                .WithMany()
                .HasForeignKey(b => b.BlockedId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DeviceToken>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Refresh token'lar hash'leriyle aranır; kullanıcı silinince düşer.
            builder.Entity<RefreshToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique();

            builder.Entity<RefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Bir film bir gruba bir kez önerilir; grup silinince öneriler de
            // düşer. Film silinemez (oy/puan gibi geçmiş ona bağlı).
            builder.Entity<EventGroupSuggestion>()
                .HasKey(s => new { s.EventGroupId, s.MovieId });

            builder.Entity<EventGroupSuggestion>()
                .HasOne(s => s.EventGroup)
                .WithMany(g => g.Suggestions)
                .HasForeignKey(s => s.EventGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<EventGroupSuggestion>()
                .HasOne(s => s.Movie)
                .WithMany()
                .HasForeignKey(s => s.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MatchingRun>()
                .Property(r => r.Status)
                .HasConversion<string>()
                .HasMaxLength(16);

            builder.Entity<MatchingRun>()
                .Property(r => r.Error)
                .HasMaxLength(500);

            builder.Entity<MatchingRun>()
                .Property(r => r.RequestedBy)
                .HasMaxLength(450);

            // İşçi sıradaki işi, uç da etkinliğin son işini bununla bulur.
            builder.Entity<MatchingRun>()
                .HasIndex(r => new { r.Status, r.CreatedAt });

            builder.Entity<MatchingRun>()
                .HasIndex(r => new { r.CinemaEventId, r.CreatedAt });

            builder.Entity<MatchingRun>()
                .HasOne(r => r.CinemaEvent)
                .WithMany()
                .HasForeignKey(r => r.CinemaEventId)
                .OnDelete(DeleteBehavior.Cascade);

            // MovieGenre için birleşik birincil anahtar (composite key) tanımlaması
            builder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });

            // UserFollow ilişkileri için OnDelete davranışını Restrict olarak ayarlama
            builder.Entity<UserFollow>()
                .HasOne(uf => uf.Follower)
                .WithMany()
                .HasForeignKey(uf => uf.FollowerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<UserFollow>()
                .HasOne(uf => uf.Following)
                .WithMany()
                .HasForeignKey(uf => uf.FollowingId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Message ilişkileri için OnDelete davranışını Restrict olarak ayarlama
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Bir Watchlist silindiğinde, Message'lardaki referansı null yapma kuralı
            builder.Entity<Message>()
                .HasOne(m => m.Watchlist)
                .WithMany()
                .HasForeignKey(m => m.WatchlistId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    } 
}