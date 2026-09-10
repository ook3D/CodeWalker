using CodeWalker.GameFiles;
using Xunit;

namespace CodeWalker.Core.Tests;

public class IndexTests
{
    [Fact]
    public void EnsurePreservesFirstValueAndExistingReturnConvention()
    {
        JenkIndex.Clear();
        StatsNames.Clear();
        GlobalText.Clear();
        try
        {
            Assert.True(JenkIndex.Ensure(""));
            Assert.False(JenkIndex.Ensure("core_audit_test"));
            Assert.True(JenkIndex.Ensure("core_audit_test"));
            Assert.Equal("core_audit_test", JenkIndex.GetString(JenkHash.GenHash("core_audit_test")));
            Assert.False(StatsNames.Ensure("first", 123));
            Assert.True(StatsNames.Ensure("second", 123));
            Assert.Equal("first", StatsNames.GetString(123));
            Assert.False(GlobalText.Ensure("first", 123));
            Assert.True(GlobalText.Ensure("second", 123));
            Assert.Equal("first", GlobalText.GetString(123));
        }
        finally
        {
            JenkIndex.Clear();
            StatsNames.Clear();
            GlobalText.Clear();
        }
    }
}
