using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Data;
using SagaOrchestrator.Models;
using SharedContracts.Events;
using System.Text.Json;

namespace SagaOrchestrator.Handlers
{
    public interface IDeadLetterQueueHandler
    {
        Task HandleFailedMessage(string message, string error, string source);
        Task ProcessDeadLetterMessages();
    }

    public class DeadLetterQueueHandler : IDeadLetterQueueHandler
    {
        private readonly ILogger<DeadLetterQueueHandler> _logger;
        private readonly SagaDbContext _dbContext;
        private readonly IBus _bus;

        public DeadLetterQueueHandler(
            ILogger<DeadLetterQueueHandler> logger,
            SagaDbContext dbContext,
            IBus bus)
        {
            _logger = logger;
            _dbContext = dbContext;
            _bus = bus;
        }

        public async Task HandleFailedMessage(string message, string error, string source)
        {
            try
            {
                var deadLetterMessage = new DeadLetterMessage
                {
                    MessageContent = message,
                    Error = error,
                    Source = source,
                    CreatedAt = DateTime.UtcNow,
                    RetryCount = 0,
                    Status = DeadLetterStatus.Pending
                };

                _dbContext.DeadLetterMessages.Add(deadLetterMessage);
                await _dbContext.SaveChangesAsync();

                _logger.LogWarning($"Message moved to dead letter queue. Source: {source}, Error: {error}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling dead letter message");
                throw;
            }
        }

        public async Task ProcessDeadLetterMessages()
        {
            try
            {
                var pendingMessages = await _dbContext.DeadLetterMessages
                    .Where(m => m.Status == DeadLetterStatus.Pending && m.RetryCount < 3)
                    .ToListAsync();

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        // Deserialize the message content based on the source
                        switch (message.Source)
                        {
                            case "OrderCreated":
                                var orderCreated = JsonSerializer.Deserialize<OrderCreated>(message.MessageContent);
                                if (orderCreated != null)
                                {
                                    await _bus.Publish(orderCreated);
                                    _logger.LogInformation($"Republished OrderCreated event for order {orderCreated.OrderId}");
                                }
                                break;

                            case "PaymentProcessed":
                                var paymentProcessed = JsonSerializer.Deserialize<PaymentProcessed>(message.MessageContent);
                                if (paymentProcessed != null)
                                {
                                    await _bus.Publish(paymentProcessed);
                                    _logger.LogInformation($"Republished PaymentProcessed event for order {paymentProcessed.OrderId}");
                                }
                                break;

                            case "InventoryUpdated":
                                var inventoryUpdated = JsonSerializer.Deserialize<InventoryUpdated>(message.MessageContent);
                                if (inventoryUpdated != null)
                                {
                                    await _bus.Publish(inventoryUpdated);
                                    _logger.LogInformation($"Republished InventoryUpdated event for order {inventoryUpdated.OrderId}");
                                }
                                break;

                            case "OrderCompensated":
                                var orderCompensated = JsonSerializer.Deserialize<OrderCompensated>(message.MessageContent);
                                if (orderCompensated != null)
                                {
                                    await _bus.Publish(orderCompensated);
                                    _logger.LogInformation($"Republished OrderCompensated event for order {orderCompensated.OrderId}");
                                }
                                break;

                            case "PaymentCompensated":
                                var paymentCompensated = JsonSerializer.Deserialize<PaymentCompensated>(message.MessageContent);
                                if (paymentCompensated != null)
                                {
                                    await _bus.Publish(paymentCompensated);
                                    _logger.LogInformation($"Republished PaymentCompensated event for order {paymentCompensated.OrderId}");
                                }
                                break;

                            case "InventoryCompensated":
                                var inventoryCompensated = JsonSerializer.Deserialize<InventoryCompensated>(message.MessageContent);
                                if (inventoryCompensated != null)
                                {
                                    await _bus.Publish(inventoryCompensated);
                                    _logger.LogInformation($"Republished InventoryCompensated event for order {inventoryCompensated.OrderId}");
                                }
                                break;

                            default:
                                _logger.LogWarning($"Unknown message source: {message.Source}");
                                break;
                        }

                        message.RetryCount++;
                        message.LastRetryAt = DateTime.UtcNow;

                        if (message.RetryCount >= 3)
                        {
                            message.Status = DeadLetterStatus.Failed;
                            _logger.LogError($"Message permanently failed after {message.RetryCount} retries. Source: {message.Source}");
                        }
                        else
                        {
                            message.Status = DeadLetterStatus.Processed;
                            _logger.LogInformation($"Message processed successfully. Source: {message.Source}");
                        }

                        await _dbContext.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing dead letter message {message.Id}");
                        message.RetryCount++;
                        message.LastRetryAt = DateTime.UtcNow;

                        if (message.RetryCount >= 3)
                        {
                            message.Status = DeadLetterStatus.Failed;
                        }

                        await _dbContext.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dead letter messages");
                throw;
            }
        }
    }
} 