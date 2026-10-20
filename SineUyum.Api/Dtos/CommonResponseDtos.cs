namespace SineUyum.Api.Dtos
{
    // Yalnızca bilgi mesajı dönen uçlar için: { "message": "..." }.
    public class StatusMessageDto
    {
        public string Message { get; set; } = string.Empty;

        public StatusMessageDto() { }
        public StatusMessageDto(string message) => Message = message;
    }

    // Film özeti; etkinlik önerilerinde, mesajdaki film önerisinde ve
    // "addmovie" yanıtında aynı şekil kullanılır.
    public class MovieSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
    }

    // Takipçi / takip edilen listelerindeki kullanıcı.
    public class UserBriefDto
    {
        public string Id { get; set; } = string.Empty;
        public string? UserName { get; set; }
    }

    public class FollowStatusDto
    {
        public bool IsFollowing { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
    }
}
