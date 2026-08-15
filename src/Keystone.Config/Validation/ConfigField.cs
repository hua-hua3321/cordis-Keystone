namespace Keystone.Config.Validation;

/// <summary>配置字段声明（08 §5：schema 声明 → 编译期校验 + 默认值补齐）。</summary>
public sealed record ConfigField(string Name, bool Required, object? Default);
