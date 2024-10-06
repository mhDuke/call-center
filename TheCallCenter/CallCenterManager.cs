namespace TheCallCenter;
public class CallerCenterManager
{
    private readonly Queue<Call> incomingCalls;
    private Queue<Agent> availableAgents;
    private readonly ICallRouter callMarshaller;

    TaskCompletionSource<Agent>? agentAwaitedByCall;
    TaskCompletionSource<Call>? callAwaitedByAgent;


    private readonly HashSet<Agent> agentsToBeRemoved;
    private int callsInProgress;
    private int callsProcessed;

    public CallerCenterManager(ICallRouter callMarshaller)
    {
        incomingCalls = [];
        availableAgents = [];
        agentsToBeRemoved = [];
        this.callMarshaller = callMarshaller ?? throw new ArgumentNullException(nameof(callMarshaller));
    }

    public int CallsInQueue => incomingCalls.Count + (agentAwaitedByCall?.Task.IsCompleted == false ? 1 : 0);
    public int CallsInProgress => callsInProgress;
    public int AvailableAgents => availableAgents.Count;
    public int CallsProcessed => callsProcessed;
    public bool IsRunning { get; private set; } = false;


    public CallerCenterManager EnqueueAgent(Agent agent)
    {
        lock (availableAgents)
        {
            //if (callsAwaitingAgents.TryDequeue(out var awaiter))
            //    awaiter.SetResult(agent);
            if (agentAwaitedByCall?.Task.IsCompleted == false)
                agentAwaitedByCall.SetResult(agent);
            else
                availableAgents.Enqueue(agent);
        }

        return this;
    }

    private Task<Agent> DequeueAgent()
    {
        if (availableAgents.TryDequeue(out var agent))
            return Task.FromResult(agent);

        agentAwaitedByCall = new();
        return agentAwaitedByCall.Task;
    }

    public void RemoveAgent(Agent agent)
    {
        lock (availableAgents)
        {
            if (availableAgents.Contains(agent))
                availableAgents = new(availableAgents.Where(a => a.Id != agent.Id));
            else
                agentsToBeRemoved.Add(agent);
        }
    }

    public CallerCenterManager EnqueueCall(Call call)
    {
        if (callAwaitedByAgent?.Task.IsCompleted == false)
            callAwaitedByAgent.SetResult(call);
        else
            incomingCalls.Enqueue(call);

        return this;
    }

    private Task<Call> DequeueCall()
    {
        if (incomingCalls.TryDequeue(out var call))
            return Task.FromResult(call);

        callAwaitedByAgent = new();
        return callAwaitedByAgent.Task;
    }

    public async Task RunCallCenter(CancellationToken cancellationToken)
    {
        if (IsRunning)
            return;

        try
        {
            IsRunning = true;
            await CallCenterProcess(cancellationToken);
        }
        finally
        {
            IsRunning = false;
        }
    }
    private async Task CallCenterProcess(CancellationToken cancellationToken)
    {
        cancellationToken.Register(() => StopWaitingForCallsAndAgents(cancellationToken));
        while (!cancellationToken.IsCancellationRequested)
        {
            var call = await DequeueCall();
            var agent = await DequeueAgent();

            //call marshalling could fail(throw exception, for example the agent is no longer available)
            Interlocked.Increment(ref callsInProgress);
            try
            {
                callMarshaller.RouteCall(call, agent, cancellationToken).ContinueWith(callRoutingTask => PutAgentBackToQueue(callRoutingTask, agent, cancellationToken));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to route the call ?? to agent {agent.Id}. reason: {e.Message}");
            }
        }
    }

    private void StopWaitingForCallsAndAgents(CancellationToken cancellationToken)
    {
        if (agentAwaitedByCall is not null && !agentAwaitedByCall.Task.IsCompleted)
            agentAwaitedByCall?.SetCanceled(cancellationToken);

        if (callAwaitedByAgent is not null && !callAwaitedByAgent.Task.IsCompleted)
            callAwaitedByAgent?.SetCanceled(cancellationToken);
    }

    private async Task PutAgentBackToQueue(Task callTask, Agent agent, CancellationToken cancellationToken)
    {
        try
        {
            await callTask;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to route the call ?? to agent {agent.Id}. reason: {e.Message}");
        }
        if (cancellationToken.IsCancellationRequested)
            return;

        Interlocked.Increment(ref callsProcessed);
        Interlocked.Decrement(ref callsInProgress);
        bool agentWasMarkedForRemoval = agentsToBeRemoved.Remove(agent);
        if (!agentWasMarkedForRemoval)
            EnqueueAgent(agent);
    }
}

public interface ICallRouter
{
    Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken);
}
public record Call(int Id);
public record Agent(int Id);

public static class CallerCenterExtensions
{

}