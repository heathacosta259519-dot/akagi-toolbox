using ContextMenuEditor.Services;
using Xunit;

namespace ContextMenuEditor.Tests;

public sealed class ClassicMenuServiceTests
{
    [Fact]
    public void Toggle_round_trip_restores_original_state()
    {
        var original = ClassicMenuService.IsEnabled();
        try
        {
            ClassicMenuService.Enable();
            Assert.True(ClassicMenuService.IsEnabled());

            ClassicMenuService.Disable();
            Assert.False(ClassicMenuService.IsEnabled());
        }
        finally
        {
            if (original)
            {
                ClassicMenuService.Enable();
            }
            else
            {
                ClassicMenuService.Disable();
            }
        }
    }
}
