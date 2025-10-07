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
        public DbSet<EventVote> EventVotes { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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