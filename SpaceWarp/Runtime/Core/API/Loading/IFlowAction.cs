#nullable enable

using System;

namespace SpaceWarp2.API.Loading;

public interface IFlowAction
{
    public string Name { get; }
    public string Description { get; }
    public void DoAction(Action resolve, Action<string> reject);
}