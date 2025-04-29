using System.ComponentModel.DataAnnotations;

namespace SagaOrchestrator.Models
{
    public class DeadLetterMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string MessageContent { get; set; }

        [Required]
        public string Error { get; set; }

        [Required]
        public string Source { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? LastRetryAt { get; set; }

        public int RetryCount { get; set; }

        public DeadLetterStatus Status { get; set; }
    }

    public enum DeadLetterStatus
    {
        Pending,
        Processed,
        Failed
    }
}
