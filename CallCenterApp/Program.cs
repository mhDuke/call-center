using TheCallCenter;
int id = 0;

int GetId() => ++id;

CallerCenterManager center = new(new ThreeSecondsCallRouter());
CancellationTokenSource cts = new();

center.RunCallCenter(cts.Token);
void PrintDashboard()
{
    Console.WriteLine($"calls in queue: {center.CallsInQueue}");
    Console.WriteLine($"calls in progress: {center.CallsInProgress}");
    Console.WriteLine($"available agents: {center.AvailableAgents}");
}

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
        return null!;

    return v switch
    {
        "c" /*call*/ => () => { center.EnqueueCall(new Call(GetId())); PrintDashboard(); },
        "a" /*agemt*/ => () => { center.EnqueueAgent(new Agent(GetId())); PrintDashboard(); },
        "r" /*dash*/ => PrintDashboard,
        "stop" => () => cts.Cancel(),
        _ => () => { }
    };
}

public class ThreeSecondsCallRouter : ICallRouter
{
    public Task RouteCall(Call call, Agent agent, CancellationToken cancellationToken)
    {
        return Task.Delay(3000, cancellationToken);
    }
}