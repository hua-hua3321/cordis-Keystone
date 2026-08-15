namespace Keystone.Core.Errors;

/// <summary>
/// Framework-level exception carrying a stable, machine-readable error code.
/// Codes come from the <see cref="ErrorCode"/> catalog (M6, doc 12 §8) and align with
/// <see cref="Contracts.TaskResult.ErrorCode"/> (doc 06 §1);
/// implementers must not rely on message text for control flow.
/// </summary>
public class KeystoneException : Exception
{
    /// <summary>Generic fallback code for the standard constructors (CA1032).</summary>
    public const string GenericCode = ErrorCode.Generic;

    public KeystoneException()
        : this(GenericCode, "Keystone framework error.")
    {
    }

    public KeystoneException(string message)
        : this(GenericCode, message)
    {
    }

    public KeystoneException(string message, Exception innerException)
        : this(GenericCode, message, innerException)
    {
    }

    public KeystoneException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public KeystoneException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable error code from the <see cref="ErrorCode"/> catalog.</summary>
    public string Code { get; }
}
