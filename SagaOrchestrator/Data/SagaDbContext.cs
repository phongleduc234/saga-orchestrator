using Microsoft.EntityFrameworkCore;
using SharedContracts.Models;

namespace SagaOrchestrator.Data
{
    public class SagaDbContext : DbContext
    {
        public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options)
        {
        }

        public DbSet<OrderSagaState> OrderSagaStates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrderSagaState>(entity =>
            {
                entity.HasKey(e => e.CorrelationId);
                entity.Property(e => e.CurrentState).HasMaxLength(64);
                entity.ToTable("OrderSagaStates");
            });
            modelBuilder.Entity<OrderSagaState>().ToTable("OrderSagaStates");
        }
    }
}
