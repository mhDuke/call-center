using System.Reflection;
using Xunit.Sdk;

namespace CallCenterTestSuite;
public class RepeatAttribute : DataAttribute
{
    private readonly int _count;

    public RepeatAttribute(int count)
    {
        _count = count;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        return Enumerable.Range(1, _count).Select(i => new object[] { i });
    }
}