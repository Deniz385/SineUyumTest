namespace SineUyum.Api.Models
{
    public enum MatchingRunStatus
    {
        Queued,
        Running,
        Succeeded,
        Failed
    }

    // Bir etkinliğin eşleştirme işi (M7). İstek yolunda çalışmaz: yönetici
    // isteği kaydı oluşturur, arka plan işçisi işler. Sunucu yeniden başlasa
    // da kayıt kalır ve iş yeniden alınır.
    public class MatchingRun
    {
        public int Id { get; set; }

        public int CinemaEventId { get; set; }
        public CinemaEvent CinemaEvent { get; set; } = null!;

        public MatchingRunStatus Status { get; set; } = MatchingRunStatus.Queued;

        public string? RequestedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }

        // Başarılıysa: kurulan grup sayısı ve gruba alınamayan katılımcılar.
        public int? GroupCount { get; set; }
        public int? UngroupedCount { get; set; }

        // Başarısızsa kullanıcıya gösterilebilir neden (iç ayrıntı içermez).
        public string? Error { get; set; }
    }
}
