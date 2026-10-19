namespace SineUyum.Api.Exceptions
{
    // Beklenen, iş kuralı kaynaklı hatalar için. Bunların mesajı kullanıcıya
    // gösterilebilir (örn. "Yeterli katılımcı yok") ve verilen HTTP durum koduyla
    // döner. Beklenmeyen diğer tüm hatalar genel 500'e düşer ve mesajı sızmaz.
    public class DomainException : Exception
    {
        public int StatusCode { get; }

        public DomainException(string message, int statusCode = StatusCodes.Status400BadRequest)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public static DomainException NotFound(string message) =>
            new(message, StatusCodes.Status404NotFound);
    }
}
