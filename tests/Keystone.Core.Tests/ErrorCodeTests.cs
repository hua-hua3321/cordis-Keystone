using System.Text.RegularExpressions;
using Keystone.Core.Errors;

namespace Keystone.Core.Tests;

public class ErrorCodeTests
{
    [Fact]
    public void All_codes_follow_KS_CATEGORY_NAME_format()
    {
        var pattern = new Regex("^KS:[A-Z]+:[A-Z_]+$", RegexOptions.CultureInvariant);

        foreach (var code in ErrorCode.All)
        {
            Assert.Matches(pattern, code);
        }
    }

    [Fact]
    public void All_contains_representative_codes_from_each_category()
    {
        Assert.Contains(ErrorCode.Generic, ErrorCode.All);
        Assert.Contains(ErrorCode.LifecycleQuiesceTimeout, ErrorCode.All);
        Assert.Contains(ErrorCode.GatingDependencyTimeout, ErrorCode.All);
        Assert.Contains(ErrorCode.ConfigValidationFailed, ErrorCode.All);
        Assert.Contains(ErrorCode.PipelineExecutionFailed, ErrorCode.All);
    }

    [Fact]
    public void IsKnown_distinguishes_defined_and_unknown_codes()
    {
        Assert.True(ErrorCode.IsKnown(ErrorCode.LifecycleInvalidState));
        Assert.False(ErrorCode.IsKnown("KS:NOPE:UNKNOWN"));
    }

    [Fact]
    public void Generic_code_matches_KeystoneException_default()
    {
        Assert.Equal(KeystoneException.GenericCode, ErrorCode.Generic);
    }
}

public class KeystoneExceptionTests
{
    [Fact]
    public void Code_is_preserved_through_throw()
    {
        var exception = new KeystoneException(ErrorCode.LifecycleQuiesceTimeout, "quiesce timed out");

        Assert.Equal(ErrorCode.LifecycleQuiesceTimeout, exception.Code);
    }

    [Fact]
    public void Default_constructors_use_generic_code()
    {
        Assert.Equal(ErrorCode.Generic, new KeystoneException().Code);
        Assert.Equal(ErrorCode.Generic, new KeystoneException("msg").Code);
        Assert.Equal(ErrorCode.Generic, new KeystoneException("msg", new InvalidOperationException()).Code);
    }

    [Fact]
    public void Inner_exception_and_message_are_preserved()
    {
        var inner = new InvalidOperationException("inner");
        var exception = new KeystoneException(ErrorCode.PipelineExecutionFailed, "outer", inner);

        Assert.Equal("outer", exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
