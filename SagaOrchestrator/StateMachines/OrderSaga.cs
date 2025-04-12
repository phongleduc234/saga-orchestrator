using MassTransit;
using SharedContracts.Events;
using SharedContracts.Models;

namespace SagaOrchestrator.StateMachines
{
    /// <summary>
    /// Orchestrates the distributed transaction flow for order processing.
    /// Coordinates activities between Order, Payment, and Inventory services.
    /// Handles both successful flows and compensating actions when failures occur.
    /// </summary>
    public class OrderSaga : MassTransitStateMachine<OrderSagaState>
    {
        public OrderSaga(ILogger<OrderSaga> logger)
        {
            logger.LogInformation("OrderSaga state machine registered");

            // Define the state property used to persist the current state of the saga
            InstanceState(x => x.CurrentState);

            // Define correlation for all events - how to match events to saga instances
            Event(() => OrderCreated, x => x.CorrelateById(context => context.Message.CorrelationId));
            Event(() => PaymentProcessed, x => x.CorrelateById(context => context.Message.CorrelationId));
            Event(() => InventoryUpdated, x => x.CorrelateById(context => context.Message.CorrelationId));
            Event(() => PaymentCompensated, x => x.CorrelateById(context => context.Message.CorrelationId));
            Event(() => InventoryCompensated, x => x.CorrelateById(context => context.Message.CorrelationId));
            Event(() => OrderCompensated, x => x.CorrelateById(context => context.Message.CorrelationId));

            // STEP 1: Initial State - Handle OrderCreated event
            Initially(
                When(OrderCreated)
                    .Then(context => {
                        // Initialize saga state with order information
                        logger.LogInformation($"Order created: {context.Message.OrderId}");
                        context.Saga.OrderId = context.Message.OrderId;
                        context.Saga.CorrelationId = context.Message.CorrelationId;
                        context.Saga.Created = DateTime.UtcNow;
                        context.Saga.PaymentCompleted = false;
                        context.Saga.InventoryUpdated = false;
                    })
                    // Move to OrderReceived state
                    .TransitionTo(OrderReceived)
                    // Request payment processing as first step in the flow
                    .Publish(context => new ProcessPaymentRequest(
                        context.Message.CorrelationId,
                        context.Message.OrderId,
                        100.0m)) // Amount hardcoded for demo purposes
            );

            // STEP 2: Payment Processing - Handle PaymentProcessed event
            During(OrderReceived,
                When(PaymentProcessed)
                    .Then(context => {
                        // Update saga state with payment result
                        logger.LogInformation($"Payment processed for order {context.Message.OrderId}: Success = {context.Message.Success}");
                        context.Saga.PaymentCompleted = context.Message.Success;
                    })
                    // If payment succeeded, continue to inventory reservation
                    .If(context => context.Message.Success,
                        x => x.TransitionTo(PaymentCompleted)
                            .Publish(context => new UpdateInventory(
                                context.Message.CorrelationId,
                                context.Message.OrderId,
                                new List<OrderItem> {
                                    new OrderItem { ProductId = "prod_123", Quantity = 2 } // Demo product data
                                })))
                    // If payment failed, initiate compensation for the order
                    .IfElse(context => !context.Message.Success,
                        x => x.TransitionTo(PaymentFailed)
                            .Publish(context => new CompensateOrder(
                                context.Message.CorrelationId,
                                context.Message.OrderId)),
                        x => x) // Empty action for else case
            );

            // STEP 3: Inventory Processing - Handle InventoryUpdated event
            During(PaymentCompleted,
                When(InventoryUpdated)
                    .Then(context => {
                        // Update saga state with inventory result
                        logger.LogInformation($"Inventory updated for order {context.Message.OrderId}: Success = {context.Message.Success}");
                        context.Saga.InventoryUpdated = context.Message.Success;
                    })
                    // If inventory succeeded, complete the order process
                    .If(context => context.Message.Success,
                        x => x.TransitionTo(OrderCompleted)
                            .Publish(context => new OrderConfirmed(
                                context.Message.CorrelationId,
                                context.Message.OrderId)))
                    // If inventory failed, initiate compensation for both payment and order
                    .IfElse(context => !context.Message.Success,
                        x => x.TransitionTo(InventoryFailed)
                            // Request payment refund
                            .Publish(context => new CompensatePayment(
                                context.Message.CorrelationId,
                                context.Message.OrderId))
                            // Request order cancellation
                            .Publish(context => new CompensateOrder(
                                context.Message.CorrelationId,
                                context.Message.OrderId)),
                        x => x) // Empty action for else case
            );

            // COMPENSATION HANDLING

            // STEP 4.1: Handle PaymentCompensated event during inventory failure
            During(InventoryFailed,
                When(PaymentCompensated)
                    .Then(context => {
                        // Log payment compensation result
                        logger.LogInformation($"Payment compensated for order {context.Message.OrderId}: Success = {context.Message.Success}");
                    })
                    // Move to failed state
                    .TransitionTo(OrderFailed)
            );

            // STEP 4.2: Handle OrderCompensated event during failure
            During(PaymentFailed, OrderFailed,
                When(OrderCompensated)
                    .Then(context => {
                        // Log order compensation result
                        logger.LogInformation($"Order compensated: {context.Message.OrderId}");
                    })
                    // Finalize the saga (mark as complete)
                    .Finalize()
            );

            // Configure the saga to be removed when finalized
            SetCompletedWhenFinalized();
        }

        // Define the possible states of the saga
        public State OrderReceived { get; private set; }
        public State PaymentCompleted { get; private set; }
        public State PaymentFailed { get; private set; }
        public State OrderCompleted { get; private set; }
        public State InventoryFailed { get; private set; }
        public State OrderFailed { get; private set; }

        // Define the events that the saga handles
        public Event<OrderCreated> OrderCreated { get; private set; }
        public Event<PaymentProcessed> PaymentProcessed { get; private set; }
        public Event<InventoryUpdated> InventoryUpdated { get; private set; }
        public Event<PaymentCompensated> PaymentCompensated { get; private set; }
        public Event<InventoryCompensated> InventoryCompensated { get; private set; }
        public Event<OrderCompensated> OrderCompensated { get; private set; }
    }
}