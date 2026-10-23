namespace SineUyum.Api.Data
{
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore;
    using SineUyum.Api.Models;

    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<AppUser>(options)
    {
        public DbSet<Movie> Movies { get; set; }
        public DbSet<Genre> Genres { get; set; } // Newly added
        public DbSet<MovieGenre> MovieGenres { get; set; } // Newly added
        public DbSet<MovieTranslation> MovieTranslations { get; set; }
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
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ReviewLike> ReviewLikes { get; set; }
        public DbSet<ReviewComment> ReviewComments { get; set; }
        public DbSet<TasteTwinSuggestion> TasteTwinSuggestions { get; set; }
        public DbSet<WatchlistMember> WatchlistMembers { get; set; }
        public DbSet<WatchlistLike> WatchlistLikes { get; set; }
        public DbSet<WatchlistSave> WatchlistSaves { get; set; }
        public DbSet<EventVote> EventVotes { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<DeviceToken> DeviceTokens { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<UserBlock> UserBlocks { get; set; }
        public DbSet<DiaryEntry> DiaryEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // One FCM token maps to one row (upserts rely on it); a user's devices
            // are removed when the user is deleted.
            builder.Entity<DeviceToken>()
                .HasIndex(d => d.Token)
                .IsUnique();

            builder.Entity<DeviceToken>()
                .HasIndex(d => d.UserId);

            // A provider purchase maps to a single row (and a single account).
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

            builder.Entity<AppUser>()
                .Property(u => u.Language)
                .HasMaxLength(8);

            // One row per pair; a reverse index for "who blocked me".
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

            // Refresh tokens are looked up by hash; removed when the user is deleted.
            builder.Entity<RefreshToken>()
                .HasIndex(t => t.TokenHash)
                .IsUnique();

            builder.Entity<RefreshToken>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // A movie is suggested to a group once; suggestions go with the group.
            // Movies cannot be deleted (history such as votes and ratings depends on them).
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
                .Property(r => r.ErrorKey)
                .HasMaxLength(100);

            builder.Entity<MatchingRun>()
                .Property(r => r.RequestedBy)
                .HasMaxLength(450);

            // The worker finds the next job, and the endpoint an event's latest job, with these.
            builder.Entity<MatchingRun>()
                .HasIndex(r => new { r.Status, r.CreatedAt });

            builder.Entity<MatchingRun>()
                .HasIndex(r => new { r.CinemaEventId, r.CreatedAt });

            builder.Entity<MatchingRun>()
                .HasOne(r => r.CinemaEvent)
                .WithMany()
                .HasForeignKey(r => r.CinemaEventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Feed: followed users' activity is paged by ActivityAt.
            builder.Entity<UserRating>()
                .HasIndex(r => new { r.UserId, r.ActivityAt });

            // One rating per user and movie (previously only checked in code;
            // two concurrent requests could create duplicates).
            builder.Entity<UserRating>()
                .HasIndex(r => new { r.UserId, r.MovieId })
                .IsUnique();

            builder.Entity<Review>()
                .HasIndex(r => new { r.UserId, r.MovieId })
                .IsUnique();

            builder.Entity<Review>()
                .HasIndex(r => new { r.MovieId, r.CreatedAt });

            builder.Entity<Review>()
                .Property(r => r.Text)
                .HasMaxLength(2000);

            builder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Film diary: a user's entries are listed by date.
            builder.Entity<DiaryEntry>()
                .HasIndex(d => new { d.UserId, d.WatchedOn });

            // Rewatches are separate entries, but the same movie twice on the
            // same day is a duplicate (also guards against concurrent requests).
            builder.Entity<DiaryEntry>()
                .HasIndex(d => new { d.UserId, d.MovieId, d.WatchedOn })
                .IsUnique();

            builder.Entity<DiaryEntry>()
                .Property(d => d.Note)
                .HasMaxLength(500);

            builder.Entity<DiaryEntry>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Movies are never deleted (history depends on them).
            builder.Entity<DiaryEntry>()
                .HasOne(d => d.Movie)
                .WithMany()
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ReviewLike>()
                .HasKey(l => new { l.ReviewId, l.UserId });

            builder.Entity<ReviewLike>()
                .HasOne(l => l.Review)
                .WithMany(r => r.Likes)
                .HasForeignKey(l => l.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReviewLike>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReviewComment>()
                .HasIndex(c => new { c.ReviewId, c.CreatedAt });

            builder.Entity<ReviewComment>()
                .Property(c => c.Text)
                .HasMaxLength(500);

            builder.Entity<ReviewComment>()
                .HasOne(c => c.Review)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ReviewComment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TasteTwinSuggestion>()
                .HasIndex(t => new { t.UserId, t.WeekStart })
                .IsUnique();

            builder.Entity<TasteTwinSuggestion>()
                .HasIndex(t => new { t.UserId, t.SuggestedUserId });

            builder.Entity<TasteTwinSuggestion>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<TasteTwinSuggestion>()
                .HasOne(t => t.SuggestedUser)
                .WithMany()
                .HasForeignKey(t => t.SuggestedUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Discovery: public lists, newest first.
            builder.Entity<Watchlist>()
                .HasIndex(w => new { w.Visibility, w.PublishedAt });

            builder.Entity<WatchlistLike>()
                .HasKey(l => new { l.WatchlistId, l.UserId });
            builder.Entity<WatchlistLike>()
                .HasIndex(l => l.CreatedAt);
            builder.Entity<WatchlistLike>()
                .HasOne(l => l.Watchlist)
                .WithMany(w => w.Likes)
                .HasForeignKey(l => l.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<WatchlistLike>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WatchlistSave>()
                .HasKey(v => new { v.WatchlistId, v.UserId });
            // "My saved lists" and "most saved this week".
            builder.Entity<WatchlistSave>()
                .HasIndex(v => new { v.UserId, v.CreatedAt });
            builder.Entity<WatchlistSave>()
                .HasIndex(v => v.CreatedAt);
            builder.Entity<WatchlistSave>()
                .HasOne(v => v.Watchlist)
                .WithMany(w => w.Saves)
                .HasForeignKey(v => v.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Entity<WatchlistSave>()
                .HasOne(v => v.User)
                .WithMany()
                .HasForeignKey(v => v.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WatchlistMember>()
                .HasKey(m => new { m.WatchlistId, m.UserId });

            // For "which lists was I invited to / am I a member of".
            builder.Entity<WatchlistMember>()
                .HasIndex(m => new { m.UserId, m.Accepted });

            builder.Entity<WatchlistMember>()
                .HasOne(m => m.Watchlist)
                .WithMany(w => w.Members)
                .HasForeignKey(m => m.WatchlistId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<WatchlistMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // If the user who added it is deleted, the movie stays in the list.
            builder.Entity<WatchlistItem>()
                .HasOne(i => i.AddedBy)
                .WithMany()
                .HasForeignKey(i => i.AddedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // A movie is in a list once (previously only checked in code).
            builder.Entity<WatchlistItem>()
                .HasIndex(i => new { i.WatchlistId, i.MovieId })
                .IsUnique();

            // One title per movie and language.
            builder.Entity<MovieTranslation>()
                .HasKey(t => new { t.MovieId, t.Language });
            builder.Entity<MovieTranslation>()
                .HasOne(t => t.Movie)
                .WithMany(m => m.Translations)
                .HasForeignKey(t => t.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // Composite primary key for MovieGenre
            builder.Entity<MovieGenre>()
                .HasKey(mg => new { mg.MovieId, mg.GenreId });

            // Restrict OnDelete for the UserFollow relationships
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
                
            // Restrict OnDelete for the Message relationships
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

            // When a Watchlist is deleted, the reference in Messages is set to null
            builder.Entity<Message>()
                .HasOne(m => m.Watchlist)
                .WithMany()
                .HasForeignKey(m => m.WatchlistId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    } 
}