using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Messaging.ServiceBus;
using Quaaly.Infrastructure.Options;
using Quaaly.Worker.Orchestration;
using Quaaly.Worker.Queue.Models;
using System.Text.Json;
using System.Runtime.Serialization;
using System.Xml;

namespace Quaaly.Worker.Queue;

/// <summary>
/// Background service that processes messages from Azure Service Bus queue.
/// Handles pull request events and routes them to the AI orchestrator.
/// </summary>
public sealed class QueueProcessorHostedService(
    ILogger<QueueProcessorHostedService> logger,
    IOptionsMonitor<ReviewerOptions> options,
    AiOrchestrator orchestrator) : BackgroundService
{
    private readonly ReviewerOptions _options = options.CurrentValue;
    private ServiceBusClient? _serviceBusClient;
    private ServiceBusProcessor? _processor;
    private JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Queue Processor starting...");

        try
        {
            // Initialize Service Bus client and processor
            await InitializeProcessorAsync(stoppingToken);

            if (_processor == null)
            {
                logger.LogError("Failed to initialize queue processor");
                return;
            }

            // Start processing messages
            await _processor.StartProcessingAsync(stoppingToken);

            logger.LogInformation(
                "Queue Processor started successfully. Listening on queue: {QueueName}",
                _options.Queue.QueueName);

            // Keep running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Queue Processor stopping due to cancellation");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error in Queue Processor");
            throw;
        }
    }

    /// <summary>
    /// Initializes the Service Bus processor with configuration.
    /// </summary>
    private Task InitializeProcessorAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ServiceBusConnectionString environment variable is not set");
        }

        if (string.IsNullOrWhiteSpace(_options.Queue.QueueName))
        {
            throw new InvalidOperationException(
                "Queue name is not configured in settings");
        }

        _serviceBusClient = new ServiceBusClient(connectionString);

        var processorOptions = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = _options.Queue.MaxConcurrentCalls,
            AutoCompleteMessages = false, // We'll complete manually after processing
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(_options.Queue.MessageLockRenewalMinutes)
        };

        _processor = _serviceBusClient.CreateProcessor(_options.Queue.QueueName, processorOptions);

        // Register message and error handlers
        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        logger.LogInformation(
            "Service Bus processor initialized for queue: {QueueName} with {MaxConcurrentCalls} max concurrent calls",
            _options.Queue.QueueName,
            _options.Queue.MaxConcurrentCalls);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes an individual message from the queue.
    /// </summary>
    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        
        logger.LogInformation(
            "Processing message {MessageId} (Delivery count: {DeliveryCount})",
            messageId,
            args.Message.DeliveryCount);

        try
        {
            // Azure DevOps service hooks use DataContractSerializer with binary XML encoding if not explicitly set to send as non-serialized string
            string? json;
            try
            {
                using var stream = new MemoryStream(args.Message.Body.ToArray());
                using var reader = XmlDictionaryReader.CreateBinaryReader(stream, XmlDictionaryReaderQuotas.Max);
                var dcs = new DataContractSerializer(typeof(string));
                json = dcs.ReadObject(reader) as string;
                if (string.IsNullOrWhiteSpace(json))
                {
                    logger.LogWarning("Message body is empty for message {MessageId}", messageId);
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                    return;
                }
            }
            catch (SerializationException)
            {
                json = args.Message.Body.ToString();
            }

            var serviceHookEvent = JsonSerializer.Deserialize<ServiceHookEvent>(json, _jsonOptions);
            if (serviceHookEvent == null)
            {
                logger.LogWarning("Serialized object is null for message {MessageId}", messageId);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            logger.LogDebug(
                "Received event type: {EventType} from message {MessageId}",
                serviceHookEvent.EventType,
                messageId);

            // Route based on event type
            await RouteEventAsync(serviceHookEvent, args.CancellationToken);

            // Complete the message
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            
            logger.LogInformation("Successfully processed message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message {MessageId}", messageId);
            
            // If delivery count is too high, dead-letter it
            if (args.Message.DeliveryCount >= _options.Queue.MaxDeliveryAttempts)
            {
                logger.LogWarning(
                    "Message {MessageId} has been delivered {DeliveryCount} times, dead-lettering",
                    messageId,
                    args.Message.DeliveryCount);
                    
                await args.DeadLetterMessageAsync(args.Message, "MaxDeliveryCountExceeded", ex.Message, args.CancellationToken);
            }
            else
            {
                // Abandon the message to retry
                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            }
        }
    }

    /// <summary>
    /// Routes service hook events to the appropriate handler.
    /// </summary>
    private async Task RouteEventAsync(ServiceHookEvent serviceHookEvent, CancellationToken cancellationToken)
    {
        switch (serviceHookEvent.EventType)
        {
            case "ms.vss-code.git-pullrequest-comment-event":
                await HandlePullRequestCommentEventAsync(serviceHookEvent, cancellationToken);
                break;

            case "git.pullrequest.created":
            case "git.pullrequest.updated":
                logger.LogDebug("Ignoring PR lifecycle event: {EventType}", serviceHookEvent.EventType);
                // These events are available but we're focusing on comment-driven interaction
                break;

            default:
                logger.LogDebug("Ignoring unknown event type: {EventType}", serviceHookEvent.EventType);
                break;
        }
    }

    /// <summary>
    /// Handles pull request comment events.
    /// </summary>
    private async Task HandlePullRequestCommentEventAsync(ServiceHookEvent serviceHookEvent, CancellationToken cancellationToken)
    {
        try
        {
            // Deserialize the resource as a PR comment event
            var resourceJson = JsonSerializer.Serialize(serviceHookEvent.Resource);
            var commentEvent = JsonSerializer.Deserialize<PullRequestCommentEventResource>(resourceJson);

            if (commentEvent == null)
            {
                logger.LogWarning("Failed to deserialize PR comment event resource");
                return;
            }

            // Process the comment event through the orchestrator
            await orchestrator.ProcessCommentEventAsync(commentEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling pull request comment event");
            throw;
        }
    }

    /// <summary>
    /// Handles errors from the Service Bus processor.
    /// </summary>
    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(
            args.Exception,
            "Error processing message. Source: {ErrorSource}, Entity Path: {EntityPath}",
            args.ErrorSource,
            args.EntityPath);

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Queue Processor stopping...");

        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        if (_serviceBusClient != null)
        {
            await _serviceBusClient.DisposeAsync();
        }

        logger.LogInformation("Queue Processor stopped");

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().Wait();
        _serviceBusClient?.DisposeAsync().AsTask().Wait();
        base.Dispose();
    }
}
