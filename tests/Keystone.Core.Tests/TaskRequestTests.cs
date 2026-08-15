using Keystone.Core.Contracts;

namespace Keystone.Core.Tests;

public class TaskRequestTests
{
    [Fact]
    public void Record_shape_carries_all_fields()
    {
        var id = TaskId.New();
        var request = new TaskRequest(id, null, "fs", "read", null, CancellationToken.None);

        Assert.Equal(id, request.TaskId);
        Assert.Null(request.ParentTaskId);
        Assert.Equal("fs", request.Capability);
        Assert.Equal("read", request.Operation);
        Assert.Equal(CancellationToken.None, request.CancellationToken);
    }

    [Fact]
    public void Child_request_carries_parent_reference()
    {
        var parent = TaskId.New();
        var child = TaskId.CreateChild();
        var request = new TaskRequest(child, parent, "fs", "read", null, CancellationToken.None);

        // 层级：子任务 TaskId 唯一，且 ParentTaskId 指向父任务（ADR-0004 跨域编排树）
        Assert.NotEqual(parent, child);
        Assert.Equal(parent, request.ParentTaskId);
    }
}

public class TaskResultTests
{
    [Fact]
    public void Completed_factory_sets_success()
    {
        var id = TaskId.New();
        var result = TaskResult.Completed(id, data: "ok");

        Assert.Equal(id, result.TaskId);
        Assert.True(result.Succeeded);
        Assert.Equal(TaskResultType.Completed, result.Type);
        Assert.Equal("ok", result.Data);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Failed_factory_sets_error_code_and_type()
    {
        var id = TaskId.New();
        var result = TaskResult.Failed(id, Errors.ErrorCode.GatingServiceNotFound, "fs service missing");

        Assert.False(result.Succeeded);
        Assert.Equal(TaskResultType.Failed, result.Type);
        Assert.Equal(Errors.ErrorCode.GatingServiceNotFound, result.ErrorCode);
        Assert.Equal("fs service missing", result.ErrorDetail);
    }

    [Fact]
    public void Cancelled_factory_sets_cancelled_type()
    {
        var id = TaskId.New();
        var result = TaskResult.Cancelled(id);

        Assert.False(result.Succeeded);
        Assert.Equal(TaskResultType.Cancelled, result.Type);
        Assert.Null(result.ErrorCode);
    }
}
