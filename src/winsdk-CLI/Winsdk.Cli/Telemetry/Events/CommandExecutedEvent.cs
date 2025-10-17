// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Diagnostics.Telemetry;
using Microsoft.Diagnostics.Telemetry.Internal;
using System.Diagnostics.Tracing;

namespace Winsdk.Cli.Telemetry.Events;

[EventData]
internal class CommandExecutedEvent : EventBase
{
    internal CommandExecutedEvent(string commandName, DateTime executedTime)
    {
        CommandName = commandName;
        ExecutedTime = executedTime;
    }

    public string CommandName { get; private set; }

    public DateTime ExecutedTime { get; private set; }

    public override PartA_PrivTags PartA_PrivTags => PrivTags.ProductAndServiceUsage;

    public override void ReplaceSensitiveStrings(Func<string?, string?> replaceSensitiveStrings)
    {
    }

    public static void Log(string commandName)
    {
        TelemetryFactory.Get<ITelemetry>().Log("CommandExecuted_Event", LogLevel.Critical, new CommandExecutedEvent(commandName, DateTime.Now));
    }
}
