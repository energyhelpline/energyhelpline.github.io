## Background

Recently, we started using [MassTransit](http://masstransit-project.com/) with RabbitMQ to provide reliable communication between different services. As part of our testing hierarchy, we wrote a small number of acceptance tests communicating over RabbitMQ, to verify that our MassTransit configuration was correct. At the other end of our testing hierarchy, it seemed more appropriate that tests at the service boundaries should make use of the MassTransit In-Memory transport.

## Should your tests use the bus?

If you have a MassTransit consumer called, say, CustomCommandConsumer, you *could* write a test calling CustomCommandConsumer.Consume(CustomCommand myCustomCommand) directly in your test, and then assert that the CustomCommandConsumer deals with the CustomCommand message appropriately. While this approach would allow you to test the behaviour of your consumer, it doesn't test that your consumer is correctly configured with MassTransit to actually receive CustomCommand messages. **That's the problem I'm going to address in this post using the in-memory transport from MassTransit - _testing that your consumer will actually receive messages_ from the bus.**

## Why use the In-Memory Transport?

The in-memory transport makes for faster-running tests, and it simplifies build / deployment pipelines as tests using the in-memory transport don't require the presence of any message broker for the tests to run. You'll see from the demo that your consumer registration code can be written independently of transport, applying equally to RabbitMQ and the In-Memory transport.

## My Demo Project
It took me a little while to get tests running with the in-memory transport, and I couldn't find many examples demonstrating this, so I thought I'd create [this demo project](https://github.com/ronanmoriarty/blog-masstransit-inmemory-testing) to illustrate the process.

I've split the code into two test fixtures - one to demonstrate sending messages to a particular endpoint, and the other to demonstrate publishing messages. For the purposes of demonstration, I haven't refactored to remove duplication, so that each test fixture can be considered in isolation. I've created a "refactored" branch that removes this duplication and goes some way to showing how you might apply these ideas in practice.

## Multithreading Concerns

These tests add messages to the bus by sending/publishing. The bus then calls the appropriate consumer passing it the new message. The sending/publishing will happen on the main test thread, but the consumer is invoked on a separate thread. So if we were to make our assertions immediately after sending a message on the main test thread, our test may or may not fail, depending on whether the consuming thread has had an opportunity to process the message yet. So, to coordinate activities between the two threads, I have a test Consumer class using ManualResetEvents. This allows me to pause execution of the main test thread after the message has been put on the bus - pausing until the consumer has had an opportunity to process the message. Upon receiving the message, the consumer first stores the message, and then calls the manualResetEvent's Set(), to indicate that the main test thread can now continue, to make it's assertions.

In practice, you're likely to be using your actual custom consumers rather than a test consumer. As such, you're unlikely to want to be adding ManualResetEvents to your consumers! To work around these multithreading issues, you may need to loop in your main test thread until your expected condition has been met, or a timeout has been reached. After your loop exits, you can then make your assertions, to see whether the expected condition has been met, or if you reached the timeout instead.

## The Consumer Factory
In my demo, I instantiate the consumers in the test, and then when configuring the receive-endpoints, I pass in the lambda:
```
consumerType => _myEventConsumer
```
I do this so that I can coordinate my main thread with the consuming thread using a shared ManualResetEvent. As indicated above, it's unlikely you'll be using ManualResetEvents in practice. You may decide use an IoC container to resolve your consumer type, as follows:
```
consumerType => _windsorContainer.Resolve(consumerType);
```
I didn't want to tie this demo to a particular IoC provider so I instantiated the consumer in the test directly.

## Setup

So my test fixture setup, for publishing messages looks as follows:

```
private const string QueueName = "myQueue";
private const string ErrorQueueName = "myQueue_error";
private IBusControl _busControl;
private Consumer<MyEvent> _myEventConsumer;
private Consumer<Fault<MyEvent>> _myEventFaultConsumer;
private readonly ManualResetEvent _manualResetEvent = new ManualResetEvent(false);

[SetUp]
public void SetUp()
{
    _myEventConsumer = new Consumer<MyEvent>(_manualResetEvent);
    _myEventFaultConsumer = new Consumer<Fault<MyEvent>>(_manualResetEvent);
    CreateBus();
    _busControl.Start();
}

private void CreateBus()
{
    _busControl = Bus.Factory.CreateUsingInMemory(ConfigureBus);
}

private void ConfigureBus(IBusFactoryConfigurator busFactoryConfigurator)
{
    busFactoryConfigurator.UseLog4Net();
    ConfigureReceiveEndpoints(busFactoryConfigurator);
}

private void ConfigureReceiveEndpoints(IBusFactoryConfigurator busFactoryConfigurator)
{
    ConfigureReceiveEndpointsToListenForMyEvent(busFactoryConfigurator);
    ConfigureReceiveEndpointToListenForFaults(busFactoryConfigurator);
}

private void ConfigureReceiveEndpointsToListenForMyEvent(IBusFactoryConfigurator busFactoryConfigurator)
{
    busFactoryConfigurator.ReceiveEndpoint(QueueName,
    receiveEndpointConfigurator =>
    {
        receiveEndpointConfigurator.Consumer(typeof(Consumer<MyEvent>), consumerType => _myEventConsumer);
    });
}

private void ConfigureReceiveEndpointToListenForFaults(IBusFactoryConfigurator busFactoryConfigurator)
{
    busFactoryConfigurator.ReceiveEndpoint(ErrorQueueName,
    receiveEndpointConfigurator =>
    {
        receiveEndpointConfigurator.Consumer(typeof(Consumer<Fault<MyEvent>>), type => _myEventFaultConsumer);
    });
}
```

Notice how most of the code above relies on the IBusFactoryConfigurator, i.e. most of the above code is transport-independent, and so could apply equally to RabbitMq instead of the in-memory transport. So, with the exception of the consumer factory lambda discussed previously, a lot of the setup code in my demo could actually form part of your system under test.

The SendTests are structured very similarly, with a small difference being that the LoopBack address is required for sending.

The Act and Assertion are as follows:
```
        [Test]
        public async Task Consumer_has_been_registered_to_receive_message()
        {
            await PublishMyEvent();
            WaitUntilConsumerHasProcessedMessageOrTimedOut(_manualResetEvent);

            Assert.That(_myEventConsumer.ReceivedMessages.Any(), Is.True);
            Assert.That(_myEventFaultConsumer.ReceivedMessages.Any(), Is.False);
        }

        private async Task PublishMyEvent()
        {
            await _busControl.Publish(new MyEvent());
        }

        private void WaitUntilConsumerHasProcessedMessageOrTimedOut(ManualResetEvent manualResetEvent)
        {
            manualResetEvent.WaitOne(TimeSpan.FromSeconds(5));
        }
```


