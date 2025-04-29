using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Models;
using SharedContracts.Events;
using SharedContracts.Models;

namespace SagaOrchestrator.Data
{
    public class SagaDbContext : DbContext
    {
        public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options)
        {
        }

        public DbSet<OrderSagaState> OrderSagaStates { get; set; }
        public DbSet<DeadLetterMessage> DeadLetterMessages { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderSagaState>(entity =>
            {
                entity.HasKey(e => e.CorrelationId);
                entity.Property(e => e.CurrentState).HasMaxLength(64);
                entity.ToTable("OrderSagaStates");
            });
            modelBuilder.Entity<DeadLetterMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("DeadLetterMessages");
            });
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.ToTable("OrderItems");
            });
        }
    }
}
