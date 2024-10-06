using TheCallCenter;
using System.Reflection.Metadata.Ecma335;

namespace CallCenterTestSuite;

public class CallCenterTests
{
    int id = 0;
    Call NewCall => new(++id);
    Agent NewAgent => new(++id);

    private static CallerCenterManager RunCallCenter(ICallRouter callRouter, CancellationToken token)
    {
        var callCenter = new CallerCenterManager(callRouter);
        callCenter.RunCallCenter(token);
        return callCenter;
    }
    private (CallerCenterManager CallCenter, Task CallCenterProcess) RunAndGetCallCenterTask(ICallRouter callRouter, CancellationToken token)
    {
        var callCenter = new CallerCenterManager(callRouter);
        var task = callCenter.RunCallCenter(token);
        return (callCenter, task);
    }
    [Fact]
    public void Queueing3CallsShouldResultIn3CallsInQueue()
    {
        //arrange
        var center = RunCallCenter(new DoesNothingCallRouter(), default);

        //act
        center.EnqueueCall(NewCall)
            .EnqueueCall(NewCall)
            .EnqueueCall(NewCall);

        //assert
        Assert.Equal(3, center.CallsInQueue);
    }

    [Fact]
    public void Queueing3AgentsShouldResultIn3AvailableAgents()
    {
        //arrange
        var center = RunCallCenter(new DoesNothingCallRouter(), default);

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
        var center = RunCallCenter(new ThreeSecondsCallRouter(), default);

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
        var center = RunCallCenter(new ThreeSecondsCallRouter(), default);

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
        var center = RunCallCenter(new ThreeSecondsCallRouter(), default);

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
    public void FailingInCallRoutingShouldNotCrashTheCallCenter(int iteration)
    {
        //arrange
        var center = RunCallCenter(new ThrowsExceptionAfterRandomDelayCallRouter(), default);

        Assert.True(center.IsRunning);

        //act
        center.EnqueueAgent(NewAgent);
        center.EnqueueCall(NewCall);

        //assert
        Assert.True(center.IsRunning);
    }
    [Fact]
    public void CancellingTheCancellationTokenShouldStopTheCallCenter()
    {
        //arrange
        CancellationTokenSource cts = new();
        var (center, task) = RunAndGetCallCenterTask(new ThreeSecondsCallRouter(), cts.Token);

        Assert.True(center.IsRunning);

        center.EnqueueAgent(NewAgent).EnqueueCall(NewCall).EnqueueCall(NewCall).EnqueueCall(NewCall)
            .EnqueueAgent(NewAgent).EnqueueCall(NewCall).EnqueueCall(NewCall).EnqueueCall(NewCall);

        //act
        cts.Cancel();

        //assert
        Assert.True(task.IsCanceled);
        Assert.False(center.IsRunning);
    }

    [Fact]
    public void StartingTheCallCenterSecondTimeShouldHaveNoEffect()
    {
        //arrange
        var (center, task) = RunAndGetCallCenterTask(new ThreeSecondsCallRouter(), default);
        //act
        var secondTask = center.RunCallCenter(default);

        //assert
        Assert.False(task.IsCompleted);
        Assert.True(secondTask.IsCompleted);
    }
    [Fact]
    public async Task RemovingInCallAgentShouldRemoveTheAgentOnceTheCallIsUp()
    {
        //arrange
        var (center, _) = RunAndGetCallCenterTask(new DefinedCallLengthCallRouter(1), default);
        var agent = NewAgent;

        //act
        center.EnqueueAgent(agent).EnqueueCall(NewCall).EnqueueCall(NewCall).EnqueueCall(NewCall);
        center.RemoveAgent(agent);
        await Task.Delay(1100);

        //assert
        Assert.Equal(2, center.CallsInQueue);
        Assert.Equal(0, center.AvailableAgents);
        Assert.Equal(1, center.CallsProcessed);
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
public class DefinedCallLengthCallRouter(int callLengthInSeconds) : ICallRouter
{
    public int CallLengthInSeconds { get; } = callLengthInSeconds;
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        return Task.Delay(CallLengthInSeconds * 1000, cancellationToken);
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
