// SineUyum.Api/Models/Message.cs
using System.ComponentModel.DataAnnotations;

namespace SineUyum.Api.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty; // Gönderen
        public AppUser Sender { get; set; } = null!;

        [Required]
        public string RecipientId { get; set; } = string.Empty; // Alan
        public AppUser Recipient { get; set; } = null!;

        [Required]
        public string Content { get; set; } = string.Empty; // Mesaj içeriği

        public DateTime? DateRead { get; set; } // Okunma tarihi (boş ise okunmamış demektir)
        
        [Required]
        public DateTime MessageSent { get; set; } = DateTime.UtcNow;
    }
}