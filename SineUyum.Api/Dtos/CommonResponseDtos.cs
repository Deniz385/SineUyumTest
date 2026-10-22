namespace SineUyum.Api.Dtos
{
    // For endpoints that only return an informational message: { "message": "..." }.
    // The message is in the request language.
    public class StatusMessageDto
    {
        public string Message { get; set; } = string.Empty;

        public StatusMessageDto() { }
        public StatusMessageDto(string message) => Message = message;
    }

    // Movie summary; the same shape is used for event suggestions, a movie
    // suggested in a message and the "addmovie" response.
    public class MovieSummaryDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? PosterPath { get; set; }
    }

    // A user in follower / following lists.
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
