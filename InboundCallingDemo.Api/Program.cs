using Azure.Communication.CallAutomation;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CallAutomation.Contracts;
using System.Text.Json;
using Azure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new CallAutomationClient(builder.Configuration["ACS:ConnectionString"]));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/incomingCall", async (EventGridEvent[] events, CallAutomationClient client) =>
{
    foreach (EventGridEvent eventGridEvent in events)
    {
        // Handle system events
        if (eventGridEvent.TryGetSystemEventData(out object eventData))
        {
            // Handle the subscription validation event
            if (eventData is SubscriptionValidationEventData subscriptionValidationEventData)
            {
                // Do any additional validation (as required) and then return back the below response
                var responseData = new
                {
                    ValidationResponse = subscriptionValidationEventData.ValidationCode
                };

                return Results.Ok(responseData);
            }
        }

        var incomingCall = JsonSerializer.Deserialize<IncomingCall>(eventGridEvent.Data);
        await client.AnswerCallAsync(incomingCall.IncomingCallContext,
            new Uri(builder.Configuration["VS_TUNNEL_URL"] + "api/callbacks"));
    }

    return Results.Ok();
});

app.MapPost("/api/callbacks", async (CloudEvent[] events, CallAutomationClient client, ILogger<Program> logger) =>
{
    foreach (var cloudEvent in events)
    {
        var callbackEvent = CallAutomationEventParser.Parse(cloudEvent);
        if (callbackEvent is CallConnected callConnected)
        {
            logger.LogInformation($"Call is connected!");
        }
    }
});

app.Run();
