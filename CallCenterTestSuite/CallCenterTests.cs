using TheCallCenter;
using System.Reflection.Metadata.Ecma335;

namespace CallCenterTestSuite;

public class CallCenterTests
{
    int id = 0;
    Call NewCall => new(++id);
    Agent NewAgent => new(++id);

    [Fact]
    public async Task Queueing3CallsShouldResultIn3CallsInQueue()
    {
        //arrange
        CallerCenterManager center = new(new DoesNothingCallRouter());
        CancellationTokenSource cts = new();
        center.RunCallCenter(cts.Token);

        //act
        center.EnqueueCall(NewCall)
            .EnqueueCall(NewCall)
            .EnqueueCall(NewCall);

        //assert
        Assert.Equal(3, center.CallsInQueue);
    }

    [Fact]
    public async Task Queueing3AgentsShouldResultIn3AvailableAgents()
    {
        //arrange
        CallerCenterManager center = new(new DoesNothingCallRouter());
        CancellationTokenSource cts = new();
        center.RunCallCenter(cts.Token);

        //act
        center.EnqueueAgent(NewAgent)
            .EnqueueAgent(NewAgent)
            .EnqueueAgent(NewAgent);

        //assert
        Assert.Equal(3, center.AvailableAgents);
    }

    [Fact]
    public async Task Queueing3CallsAndOneAgentShouldResultIn3ProcessedCallsAfter9Seconds()
    {
        //arrange
        CallerCenterManager center = new(new ThreeSecondsCallRouter());
        CancellationTokenSource cts = new();
        center.RunCallCenter(cts.Token);

        //act

        //queue 3 calls
        center.EnqueueCall(NewCall)
            .EnqueueCall(NewCall)
            .EnqueueCall(NewCall);

        //queue 1 agent
        center.EnqueueAgent(NewAgent);

        //assert
        Assert.Equal(0, center.CallsProcessed);
        Assert.Equal(2, center.CallsInQueue);
        Assert.Equal(1, center.CallsInProgress);
        Assert.Equal(0, center.AvailableAgents);

        //wait 9 seonds
        await Task.Delay(9100);
        Assert.Equal(3, center.CallsProcessed);
        Assert.Equal(0, center.CallsInQueue);
        Assert.Equal(0, center.CallsInProgress);
        Assert.Equal(1, center.AvailableAgents);
    }
    [Fact]
    public async Task QueueingOneAgentAnd3CallsShouldResultIn3ProcessedCallsAfter9Seconds()
    {
        //arrange
        CallerCenterManager center = new(new ThreeSecondsCallRouter());
        CancellationTokenSource cts = new();
        center.RunCallCenter(cts.Token);

        //act

        //queue 1 agent
        center.EnqueueAgent(NewAgent);

        //queue 3 calls
        center.EnqueueCall(NewCall)
            .EnqueueCall(NewCall)
            .EnqueueCall(NewCall);

        //assert
        Assert.Equal(0, center.CallsProcessed);
        Assert.Equal(2, center.CallsInQueue);
        Assert.Equal(1, center.CallsInProgress);
        Assert.Equal(0, center.AvailableAgents);

        //wait 9 seonds
        await Task.Delay(9100);
        Assert.Equal(3, center.CallsProcessed);
        Assert.Equal(0, center.CallsInQueue);
        Assert.Equal(0, center.CallsInProgress);
        Assert.Equal(1, center.AvailableAgents);
    }
    
    [Fact]
    public async Task Queueing3CallsAnd3AgentsShouldResultIn3ProcessedCallsAfter3Seconds()
    {
        //arrange
        CallerCenterManager center = new(new ThreeSecondsCallRouter());
        CancellationTokenSource cts = new();
        center.RunCallCenter(cts.Token);

        //act

        //queue 3 calls
        center.EnqueueCall(NewCall)
            .EnqueueCall(NewCall)
            .EnqueueCall(NewCall);

        //queue 1 agent
        center.EnqueueAgent(NewAgent)
            .EnqueueAgent(NewAgent)
            .EnqueueAgent(NewAgent);

        //assert
        Assert.Equal(0, center.CallsProcessed);
        Assert.Equal(0, center.CallsInQueue);
        Assert.Equal(3, center.CallsInProgress);
        Assert.Equal(0, center.AvailableAgents);

        //wait 9 seonds
        await Task.Delay(3100);
        Assert.Equal(3, center.CallsProcessed);
        Assert.Equal(0, center.CallsInQueue);
        Assert.Equal(0, center.CallsInProgress);
        Assert.Equal(3, center.AvailableAgents);
    }

    [Theory]
    [Repeat(10)]
    public async Task FailingInCallRoutingShouldNotCrashTheCallCenter(int iteration)
    {
        //arrange
        CallerCenterManager center = new(new ThrowsExceptionAfterRandomDelayCallRouter());
        CancellationTokenSource cts = new();

        center.RunCallCenter(cts.Token);
        Assert.True(center.IsRunning);

        //act
        center.EnqueueAgent(NewAgent);
        center.EnqueueCall(NewCall);

        //assert
        Assert.True(center.IsRunning);
    }
    [Fact]
    public async Task CancellingTheCancellationTokenShouldStopTheCallCenter()
    {
        //arrange
        CallerCenterManager center = new(new ThreeSecondsCallRouter());
        CancellationTokenSource cts = new();

        var task = center.RunCallCenter(cts.Token);
        Assert.True(center.IsRunning);

        center.EnqueueAgent(NewAgent).EnqueueCall(NewCall).EnqueueCall(NewCall).EnqueueCall(NewCall)
            .EnqueueAgent(NewAgent).EnqueueCall(NewCall).EnqueueCall(NewCall).EnqueueCall(NewCall);

        //act
        cts.Cancel();

        //assert
        Assert.True(task.IsCanceled);
        Assert.False(center.IsRunning);
    }
}

public class DoesNothingCallRouter : ICallRouter
{
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class ThreeSecondsCallRouter : ICallRouter
{
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        return Task.Delay(3000, cancellationToken);
    }
}

public class ThrowsExceptionAfterRandomDelayCallRouter : ICallRouter
{
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        int randomNumber = GetRandomNumber();
        if (randomNumber == 0)
            JustThrow();

        return ThrowAfterDelay(randomNumber, cancellationToken);
    }
    private static async Task ThrowAfterDelay(int randomNumber, CancellationToken cancellationToken)
    {
        await Task.Delay(randomNumber * 1000, cancellationToken);
        throw new Exception("thrown after a delay");
    }
    private static void JustThrow() => throw new Exception("Thrown right away");
    private static readonly Random random = new();
    private static int GetRandomNumber()
    {
        return random.Next(0, 2); //The upper bound is exclusive.
    }
}
public class FromOneTo5SeondsCallRouter : ICallRouter
{
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        int randomNumber = GetRandomNumber();
        if (randomNumber == 5)
            throw new Exception("Call routing timed out");

        return Task.Delay(randomNumber * 1000, cancellationToken);
    }

    private static readonly Random random = new();
    private static int GetRandomNumber()
    {
        return random.Next(1, 6);
    }
}