using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using System.Diagnostics;
using RabbitMQ.Client;
using System.Text;
using OpenTelemetry.Trace;
using RabbitMQ.Client.Events;

namespace ObservabilitySample;

public class RabbitMqObservabilityFeatures
{
    private static ActivitySource _activity = new("Rabbit-MQ", "1.0.0");
    private static readonly TextMapPropagator _propagator = Propagators.DefaultTextMapPropagator;

    private static IEnumerable<string> ExtractTraceContextFromBasicProperties(IBasicProperties props, string key)
    {
        try
        {
            if (props.Headers.TryGetValue(key, out var value))
            {
                var bytes = value as byte[];
                return [Encoding.UTF8.GetString(bytes)];
            }
        }

        catch (Exception)
        {
            throw;
        }

        return [];
    }


    private async Task PublishEvent()
    {
        using var activity = _activity?.StartActivity("RabbitMq Publish", ActivityKind.Producer);

        // Call AddActivityToHeaders() and add headers to Event.
        // which you should catch them in the reciveEvent
        // publish event through rabbit mq or masstransit
    }
    private async Task RecieveEvent(object sender, BasicDeliverEventArgs eventArgs)
    {
        var parentContext = _propagator.Extract(default, eventArgs.BasicProperties, ExtractTraceContextFromBasicProperties);

        //Start Existing Activity Left From Event Publisher
        using var activity = _activity?.StartActivity("Process Message", ActivityKind.Consumer, parentContext.ActivityContext);

        string eventName = eventArgs.RoutingKey;
        string message = Encoding.UTF8.GetString(eventArgs.Body.Span);
        // adding headers comming from event to Activity
        AddActivityToHeader(activity!, eventName);
        try
        {
            // processing events.
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, message: ex.Message);
            // catching exceptions in event consumers.
            
            activity.SetStatus(ActivityStatusCode.Error);
            // store the error details in span information
            activity.RecordException(ex);
        }
    }


    /// <summary>
    /// Activity Headers should be added in 
    /// </summary>
    /// <param name="activity"></param>
    /// <param name="eventName"></param>
    /// <param name="props"></param>
    private void AddActivityToHeader(Activity activity, string eventName, IBasicProperties props = null!)
    {
        if (activity is null)
            _activity = new ActivitySource("Rabbit-MQ", "1.0.0");

        if (activity is not null)
        {
            _propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props, InjectContextIntoHeader);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination_kind", "queue");
            activity?.SetTag("messaging.rabbitmq.event", eventName);
        }
        else
        {
            // throwing or logging error.
        }
    }
    private void InjectContextIntoHeader(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object>();
            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            // throwing or logging error.
        }
    }
}
