using TheCallCenter;
int id = 0;

int GetId() => ++id;

CallerCenterManager center = new(new DefinedCallLengthCallRouter(3));
CancellationTokenSource cts = new();

void PrintDashboard()
{
    Console.WriteLine($"calls in queue: {center.CallsInQueue}");
    Console.WriteLine($"calls in progress: {center.CallsInProgress}");
    Console.WriteLine($"available agents: {center.AvailableAgents}");
    Console.WriteLine($"calls processed: {center.CallsProcessed}");
}

List<Agent> agents = [];
while (!cts.Token.IsCancellationRequested)
{
    var action = ProcessUserInput(Console.ReadLine());

    if (action is not null)
    {
        //PrintDashboard();
        action();
    }
}
Action ProcessUserInput(string? v)
{
    if (string.IsNullOrWhiteSpace(v))
        return PrintDashboard;

    return v switch
    {
        "c" /*call*/ => () => { center.EnqueueCall(new Call(GetId())); PrintDashboard(); },
        "a" /*agent*/ => () => { Agent a = new(GetId()); agents.Add(a); center.EnqueueAgent(a); PrintDashboard(); },
        "r" /*remove*/ => () => { var a = agents[0]; center.RemoveAgent(a); agents.Remove(a); PrintDashboard(); },
        "stop" => () => cts.Cancel(),
        "start" => () => { center.RunCallCenter(cts.Token); PrintDashboard(); },
        _ => () => { }
    };
}

public class DefinedCallLengthCallRouter(int callLengthInSeconds) : ICallRouter
{
    public int CallLengthInSeconds { get; } = callLengthInSeconds;
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        return Task.Delay(CallLengthInSeconds * 1000, cancellationToken);
    }
}