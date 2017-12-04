---
layout: post
title: Using the In-Memory Transport to Test your MassTransit Configuration
author: Ronan Moriarty
excerpt: Shows how to use MassTransit's In-Memory transport to test your consumer registration
---

## Background

Recently, we started using [MassTransit](http://masstransit-project.com/) with RabbitMQ to provide reliable communication between different services. As part of our testing hierarchy, we wrote a small number of acceptance tests communicating over RabbitMQ, to verify that our MassTransit configuration was correct. At the other end of our testing hierarchy, it seemed more appropriate that tests at the service boundaries should make use of the MassTransit In-Memory transport.

## Should your tests use the bus?

If you have a MassTransit consumer called, say, CustomCommandConsumer, you *could* write a test calling CustomCommandConsumer.Consume(CustomCommand myCustomCommand) directly in your test, and then assert that the CustomCommandConsumer deals with the CustomCommand appropriately. While this approach would allow you to test the behaviour of your consumer, it doesn't test that your consumer is correctly configured with MassTransit to actually receive CustomCommands. **That's the problem I'm going to address in this post using the in-memory transport from MassTransit - _testing that your consumer will actually receive commands and events_ from the bus.**

## Why use the In-Memory Transport?

The in-memory transport makes for faster-running tests, and it simplifies build / deployment pipelines as tests using the in-memory transport don't require the presence of any message broker for the tests to run. You'll see from the demo that your consumer registration code can be written independently of transport, applying equally to RabbitMQ and the In-Memory transport.

## My Demo Project

It took me a little while to get tests running with the in-memory transport, and I couldn't find many examples demonstrating this, so I thought I'd create [this demo project](https://github.com/ronanmoriarty/blog-masstransit-inmemory-testing) to illustrate the process.

I've split the code into two test fixtures - one to demonstrate sending commands to a particular endpoint, and the other to demonstrate publishing events.

## Multithreading Concerns

These tests send commands and publish events to the bus. The bus then calls the appropriate consumer passing it the new command / event. The sending / publishing will happen on the main test thread, but the consumer is invoked on a separate thread. So if we were to make our assertions immediately after sending a command on the main test thread, our test may or may not fail, depending on whether the consuming thread has had an opportunity to process the command yet. So, to get around these issues, we loop in your main test thread until our expected condition has been met, or a timeout has been reached. We do this as follows:

```c#
private void WaitUntilConditionMetOrTimedOut(Func<bool> conditionMet)
{
    var timeoutExpired = false;
    var startTime = DateTime.Now;
    while (!conditionMet() && !timeoutExpired)
    {
        Thread.Sleep(100);
        timeoutExpired = DateTime.Now - startTime > TimeSpan.FromSeconds(5);
    }
}
```

I've chosen to just capture the received events in my event consumer, so in my test after I publish the event, I wait at most 5 seconds to see if an event has been received:
```c#
    WaitUntilConditionMetOrTimedOut(() => State.EventsReceived.Any());
```

After giving the consumer a chance to process the event, we're ready to make our assertions.

## The System Under Test

I've defined the following class to configure what consumers listen on which queues:
```c#
public class BusFactoryConfiguration
{
    private readonly IConsumerFactory _consumerFactory;
    private const string QueueName = "myQueue";
    private const string ErrorQueueName = "myQueue_error";

    public BusFactoryConfiguration(IConsumerFactory consumerFactory)
    {
        _consumerFactory = consumerFactory;
    }

    public void Configure(IBusFactoryConfigurator busFactoryConfigurator)
    {
        busFactoryConfigurator.UseLog4Net();
        ConfigureReceiveEndpoints(busFactoryConfigurator);
    }

    private void ConfigureReceiveEndpoints(IBusFactoryConfigurator busFactoryConfigurator)
    {
        ConfigureConsumersListeningOnMainQueue(busFactoryConfigurator);
        ConfigureConsumersListeningOnErrorQueue(busFactoryConfigurator);
    }

    private void ConfigureConsumersListeningOnMainQueue(IBusFactoryConfigurator busFactoryConfigurator)
    {
        busFactoryConfigurator.ReceiveEndpoint(QueueName,
            receiveEndpointConfigurator =>
            {
                receiveEndpointConfigurator.Consumer(typeof(MyCommandConsumer), _consumerFactory.Create);
                receiveEndpointConfigurator.Consumer(typeof(MyEventConsumer), _consumerFactory.Create);
            });
    }

    private void ConfigureConsumersListeningOnErrorQueue(IBusFactoryConfigurator busFactoryConfigurator)
    {
        busFactoryConfigurator.ReceiveEndpoint(ErrorQueueName,
            receiveEndpointConfigurator =>
            {
                receiveEndpointConfigurator.Consumer(typeof(MyCommandFaultConsumer), _consumerFactory.Create);
                receiveEndpointConfigurator.Consumer(typeof(MyEventFaultConsumer), _consumerFactory.Create);
            });
    }
}
```
Notice how the above code configures endpoints using the IBusFactoryConfigurator, which is independent of the in-memory transport and RabbitMQ.

## The Consumer Factory

The above code makes use of the IConsumerFactory interface, which I define as follows:
```c#
public interface IConsumerFactory
{
    object Create(Type typeToCreate);
}
```

My tests use a simple implementation that relies on the consumer having a blank constructor:
```c#
public class DefaultConstructorConsumerFactory : IConsumerFactory
{
    public object Create(Type typeToCreate)
    {
        return Activator.CreateInstance(typeToCreate);
    }
}
```

You may decide use an IoC container to resolve your consumer type:
```c#
public class WindsorConsumerFactory : IConsumerFactory
{
    private readonly IWindsorContainer _windsorContainer;

    public WindsorConsumerFactory(IWindsorContainer windsorContainer)
    {
        _windsorContainer = windsorContainer;
    }

    public object Create(Type typeToCreate)
    {
        return _windsorContainer.Resolve(typeToCreate);
    }
}
```
The above windsor-implementation is included in the project for illustration purposes, but not actually used in my tests.

## Setup

So my test fixture setup, for publishing events looks as follows:
```c#
private IBusControl _busControl;
private IConsumerFactory _consumerFactory;
private BusFactoryConfiguration _busFactoryConfiguration;

[SetUp]
public void SetUp()
{
    _consumerFactory = new DefaultConstructorConsumerFactory();
    _busFactoryConfiguration = new BusFactoryConfiguration(_consumerFactory);
    CreateBus();
    _busControl.Start();
}

private void CreateBus()
{
    _busControl = Bus.Factory.CreateUsingInMemory(_busFactoryConfiguration.Configure);
}
```

The SendTests are structured very similarly, with a small difference being that the LoopBack address is required for sending.

The Act and Assertion are as follows:
```c#
[Test]
public async Task Consumer_has_been_registered_to_receive_event()
{
    await PublishMyEvent();
    WaitUntilConditionMetOrTimedOut(() => State.EventsReceived.Any());

    Assert.That(State.EventsReceived.Count, Is.EqualTo(1));
    Assert.That(State.EventFaultsReceived.Count, Is.EqualTo(0));
}

private async Task PublishMyEvent()
{
    await _busControl.Publish(new MyEvent());
}
```

## Summary

Depending on your testing strategy and what acceptance tests you've written, you may need some additional tests specifically around your MassTransit configuration. This blog and the associated GitHub repository demonstrate one way to do this, using the In-Memory transport.

I've used a state-based approach for assertions in this blog, but the IConsumerFactory could instead be implemented as a separate class to return consumer instances that were previously instantiated in your tests, allowing for a more mock-based approach. Hopefully you found this useful. If you have any questions or feedback, good or bad, I'd be very happy to hear it.