namespace Keystone.Config.Entries;

/// <summary>isolate 声明档位（对齐 Cordis Dict&lt;name → true|"label"&gt;，18 §2 CA-1 第 0 步）。</summary>
public enum IsolateKind
{
    /// <summary>显式解除：分层补丁撤销底层 isolate 声明（合并时按名移除；未合并的原始层可携带）。</summary>
    None,

    /// <summary>条目私有域（true）：realm 后缀 #entryId / 组谱系 #groupId。</summary>
    Private,

    /// <summary>命名共享域（"label"）：realm 后缀 @label，同 label 条目共享该服务 scope。</summary>
    Shared,
}
