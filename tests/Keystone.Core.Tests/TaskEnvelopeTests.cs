using Keystone.Core.Contracts;
using MessagePack;

namespace Keystone.Core.Tests;

/// <summary>能力域载荷原型（P1 示例；真实能力域在 P8 定义并注册具体序列化契约）。</summary>
[MessagePackObject]
public sealed record EchoPayload
{
    [Key(0)]
    public string? Text { get; init; }
}

public class TaskEnvelopeTests
{
    [Fact]
    public void MessagePack_roundtrips_envelope()
    {
        var envelope = new TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            ParentTaskId = Guid.NewGuid(),
            Capability = "fs",
            Operation = "read",
            PayloadBytes = [1, 2, 3],
        };

        var bytes = MessagePackSerializer.Serialize(envelope);
        var restored = MessagePackSerializer.Deserialize<TaskEnvelope>(bytes);

        Assert.Equal(envelope.TaskId, restored.TaskId);
        Assert.Equal(envelope.ParentTaskId, restored.ParentTaskId);
        Assert.Equal(envelope.Capability, restored.Capability);
        Assert.Equal(envelope.Operation, restored.Operation);
        Assert.Equal(envelope.PayloadBytes, restored.PayloadBytes);
    }

    [Fact]
    public void PayloadBytes_carries_concrete_payload()
    {
        var payload = new EchoPayload { Text = "hello keystone" };
        var payloadBytes = MessagePackSerializer.Serialize(payload);

        var envelope = new TaskEnvelope
        {
            TaskId = Guid.NewGuid(),
            Capability = "echo",
            Operation = "echo",
            PayloadBytes = payloadBytes,
        };

        var restored = MessagePackSerializer.Deserialize<EchoPayload>(envelope.PayloadBytes);

        Assert.Equal("hello keystone", restored.Text);
    }

    [Fact]
    public void FromRequest_maps_fields_and_carries_payload_bytes()
    {
        var taskId = TaskId.New();
        var parentId = TaskId.New();
        var payload = new EchoPayload { Text = "x" };
        var request = new TaskRequest(taskId, parentId, "echo", "echo", payload, CancellationToken.None);
        var payloadBytes = MessagePackSerializer.Serialize(payload);

        var envelope = TaskEnvelope.FromRequest(request, payloadBytes);

        Assert.Equal(taskId.Value, envelope.TaskId);
        Assert.Equal(parentId.Value, envelope.ParentTaskId);
        Assert.Equal("echo", envelope.Capability);
        Assert.Equal("echo", envelope.Operation);
        Assert.Equal(payloadBytes, envelope.PayloadBytes);
    }
}

public class TaskResultEnvelopeTests
{
    [Fact]
    public void MessagePack_roundtrips_result_envelope()
    {
        var envelope = new TaskResultEnvelope
        {
            TaskId = Guid.NewGuid(),
            Succeeded = false,
            Type = TaskResultType.Failed,
            ErrorCode = Errors.ErrorCode.PipelineMiddlewareRejected,
            ErrorDetail = "middleware vetoed",
            DataBytes = [],
        };

        var bytes = MessagePackSerializer.Serialize(envelope);
        var restored = MessagePackSerializer.Deserialize<TaskResultEnvelope>(bytes);

        Assert.Equal(envelope.TaskId, restored.TaskId);
        Assert.False(restored.Succeeded);
        Assert.Equal(TaskResultType.Failed, restored.Type);
        Assert.Equal(Errors.ErrorCode.PipelineMiddlewareRejected, restored.ErrorCode);
        Assert.Equal("middleware vetoed", restored.ErrorDetail);
    }

    [Fact]
    public void FromResult_maps_fields_and_carries_data_bytes()
    {
        var id = TaskId.New();
        var result = TaskResult.Failed(id, Errors.ErrorCode.PipelineExecutionFailed, "boom");
        var dataBytes = new byte[] { 9, 9 };

        var envelope = TaskResultEnvelope.FromResult(result, dataBytes);

        Assert.Equal(id.Value, envelope.TaskId);
        Assert.False(envelope.Succeeded);
        Assert.Equal(TaskResultType.Failed, envelope.Type);
        Assert.Equal(Errors.ErrorCode.PipelineExecutionFailed, envelope.ErrorCode);
        Assert.Equal("boom", envelope.ErrorDetail);
        Assert.Equal(dataBytes, envelope.DataBytes);
    }
}
