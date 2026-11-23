using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TokenDashboardCompanion.Tests;

[TestClass]
public class LaunchAtLoginTests
{
    [TestMethod]
    public void PortableCommand_QuotesPathAndMarksStartupLaunch()
    {
        const string executable = @"C:\Users\Howard Mei\Apps\TokenDashboardCompanion.exe";
        Assert.AreEqual(
            "\"C:\\Users\\Howard Mei\\Apps\\TokenDashboardCompanion.exe\" --startup",
            LaunchAtLogin.PortableCommand(executable));
    }

    [TestMethod]
    public void PortableCommandMatch_DetectsMovedOrStaleRegistration()
    {
        const string current = @"D:\Apps\TokenDashboardCompanion.exe";
        Assert.IsTrue(LaunchAtLogin.IsCurrentPortableCommand(
            "\"D:\\Apps\\TokenDashboardCompanion.exe\" --startup",
            current));
        Assert.IsTrue(LaunchAtLogin.IsCurrentPortableCommand(
            "\"d:\\apps\\token-dashboardcompanion.exe\" --startup",
            current));
        Assert.IsFalse(LaunchAtLogin.IsCurrentPortableCommand(
            "\"C:\\Old\\TokenDashboardCompanion.exe\" --startup",
            current));
        Assert.IsFalse(LaunchAtLogin.IsCurrentPortableCommand(null, current));
    }
}
