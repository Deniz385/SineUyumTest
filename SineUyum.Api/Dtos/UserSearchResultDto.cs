namespace SineUyum.Api.Dtos
{
    public class UserSearchResultDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? ProfileImageUrl { get; set; }
        public string? Bio { get; set; }
        public double CompatibilityScore { get; set; }
        public int CommonMovieCount { get; set; }
    }
}
