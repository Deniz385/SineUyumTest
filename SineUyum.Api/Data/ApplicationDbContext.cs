namespace SineUyum.Api.Data
{
    
    using Microsoft.AspNetCore.Identity.EntityFrameworkCore; 
    using Microsoft.EntityFrameworkCore;
    using SineUyum.Api.Models;

    
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<AppUser>(options)
    {
        public DbSet<Movie> Movies { get; set; }
        public DbSet<UserRating> UserRatings { get; set; }
    }
}
