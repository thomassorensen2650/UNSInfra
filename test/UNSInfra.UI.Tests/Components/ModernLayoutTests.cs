using UNSInfra.UI.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using Moq;

namespace UNSInfra.UI.Tests.Components;

public class ModernLayoutTests : UITestContext
{
    public ModernLayoutTests()
    {
        // Setup navigation manager mock
        var mockNavManager = new Mock<NavigationManager>();
        mockNavManager.Setup(x => x.Uri).Returns("https://localhost/");
        mockNavManager.Setup(x => x.BaseUri).Returns("https://localhost/");
        Services.AddSingleton(mockNavManager.Object);
    }

    [Fact]
    public void ModernLayout_RendersCorrectly()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        Assert.Contains("UNS Infrastructure", component.Markup);
        Assert.Contains("modern-layout", component.Markup);
        Assert.Contains("top-navbar", component.Markup);
    }

    [Fact]
    public void ModernLayout_DisplaysBrandLogo()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        Assert.Contains("bi-diagram-3-fill", component.Markup);
        Assert.Contains("navbar-brand", component.Markup);
        
        var brandLink = component.Find("a.navbar-brand");
        Assert.Equal("/", brandLink.GetAttribute("href"));
    }

    [Fact]
    public void ModernLayout_DisplaysNavigationItems()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        Assert.Contains("Data Model", component.Markup);
        Assert.Contains("Ingress", component.Markup);
        Assert.Contains("Egress", component.Markup);
        
        // Check navigation links
        var dataModelLink = component.Find("a[href='data-model']");
        Assert.Contains("Data Model", dataModelLink.TextContent);
        
        var ingressLink = component.Find("a[href='ingress']");
        Assert.Contains("Ingress", ingressLink.TextContent);
        
        var egressLink = component.Find("a[href='egress']");
        Assert.Contains("Egress", egressLink.TextContent);
    }

    [Fact]
    public void ModernLayout_DisplaysActionIcons()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        // Connection Status icon
        var statusIcon = component.Find("a[href='/connection-status']");
        Assert.Contains("bi-activity", statusIcon.InnerHtml);
        Assert.Equal("Connection Status", statusIcon.GetAttribute("title"));

        // Logs icon
        var logsIcon = component.Find("a[href='/logs']");
        Assert.Contains("bi-journal-text", logsIcon.InnerHtml);
        Assert.Equal("Logs", logsIcon.GetAttribute("title"));

        // Settings icon
        var settingsIcon = component.Find("a[href='/settings']");
        Assert.Contains("bi-gear", settingsIcon.InnerHtml);
        Assert.Equal("Settings", settingsIcon.GetAttribute("title"));
    }

    [Fact]
    public void ModernLayout_DisplaysThemeToggle()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        var themeToggle = component.Find("button.theme-toggle");
        Assert.Equal("Toggle theme", themeToggle.GetAttribute("title"));
        Assert.Contains("bi-moon", themeToggle.InnerHtml); // Default light theme shows moon icon
    }

    [Fact]
    public void ModernLayout_ThemeToggle_ChangesIcon()
    {
        // Act
        var component = RenderComponent<ModernLayout>();
        var themeToggle = component.Find("button.theme-toggle");
        
        // Click theme toggle
        themeToggle.Click();

        // Assert
        // After clicking, should show sun icon (dark theme active)
        Assert.Contains("bi-sun", component.Markup);
    }

    [Fact]
    public void ModernLayout_DisplaysMobileMenuToggle()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        var mobileToggle = component.Find("button.mobile-menu-toggle");
        Assert.Equal("Toggle menu", mobileToggle.GetAttribute("title"));
        
        var hamburgerLines = component.FindAll(".hamburger-line");
        Assert.Equal(3, hamburgerLines.Count);
    }

    [Fact]
    public void ModernLayout_MobileMenu_TogglesCorrectly()
    {
        // Act
        var component = RenderComponent<ModernLayout>();
        var mobileToggle = component.Find("button.mobile-menu-toggle");
        
        // Initially mobile menu should be closed
        var mobileSidebar = component.Find(".mobile-sidebar");
        Assert.DoesNotContain("open", mobileSidebar.ClassList);

        // Click to open mobile menu
        mobileToggle.Click();

        // Assert mobile menu is open
        mobileSidebar = component.Find(".mobile-sidebar");
        Assert.Contains("open", mobileSidebar.ClassList);
    }

    [Fact]
    public void ModernLayout_MobileMenu_DisplaysNavigationItems()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert mobile navigation items exist
        var mobileNavItems = component.FindAll(".mobile-nav-item");
        Assert.Equal(6, mobileNavItems.Count); // Data Model, Ingress, Egress, Status, Logs, Settings

        // Check specific mobile nav items
        Assert.Contains("Data Model", component.Markup);
        Assert.Contains("Status", component.Markup);
        Assert.Contains("Logs", component.Markup);
        Assert.Contains("Settings", component.Markup);
    }

    [Fact]
    public void ModernLayout_MobileMenu_ClosesOnOverlayClick()
    {
        // Act
        var component = RenderComponent<ModernLayout>();
        var mobileToggle = component.Find("button.mobile-menu-toggle");
        
        // Open mobile menu
        mobileToggle.Click();
        
        // Click overlay to close
        var overlay = component.Find(".mobile-sidebar-overlay");
        overlay.Click();

        // Assert mobile menu is closed
        var mobileSidebar = component.Find(".mobile-sidebar");
        Assert.DoesNotContain("open", mobileSidebar.ClassList);
    }

    [Fact]
    public void ModernLayout_MobileMenu_ClosesOnCloseButton()
    {
        // Act
        var component = RenderComponent<ModernLayout>();
        var mobileToggle = component.Find("button.mobile-menu-toggle");
        
        // Open mobile menu
        mobileToggle.Click();
        
        // Click close button
        var closeButton = component.Find("button.close-sidebar");
        closeButton.Click();

        // Assert mobile menu is closed
        var mobileSidebar = component.Find(".mobile-sidebar");
        Assert.DoesNotContain("open", mobileSidebar.ClassList);
    }

    [Fact]
    public void ModernLayout_DefaultTheme_IsLight()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        var layoutDiv = component.Find(".modern-layout");
        Assert.Equal("light", layoutDiv.GetAttribute("data-theme"));
    }

    [Fact]
    public void ModernLayout_MainContent_RendersBodyContent()
    {
        // Arrange
        var testContent = "<div>Test Content</div>";

        // Act
        var component = RenderComponent<ModernLayout>(parameters => parameters
            .AddChildContent(testContent));

        // Assert
        Assert.Contains("Test Content", component.Markup);
        Assert.Contains("main-content", component.Markup);
        Assert.Contains("content-container", component.Markup);
    }

    [Fact]
    public void ModernLayout_ErrorUI_IsPresent()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert
        Assert.Contains("blazor-error-ui", component.Markup);
        Assert.Contains("An unhandled error has occurred", component.Markup);
        Assert.Contains("Reload", component.Markup);
    }

    [Fact]
    public void ModernLayout_NavigationIcons_HaveCorrectBootstrapIcons()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert navigation icons
        Assert.Contains("bi-diagram-3", component.Markup); // Data Model icon
        Assert.Contains("bi-arrow-down-circle", component.Markup); // Ingress icon
        Assert.Contains("bi-arrow-up-circle", component.Markup); // Egress icon
        Assert.Contains("bi-activity", component.Markup); // Status icon
        Assert.Contains("bi-journal-text", component.Markup); // Logs icon
        Assert.Contains("bi-gear", component.Markup); // Settings icon
    }

    [Fact]
    public void ModernLayout_ResponsiveDesign_HasCorrectClasses()
    {
        // Act
        var component = RenderComponent<ModernLayout>();

        // Assert responsive classes exist
        Assert.Contains("navbar-nav-desktop", component.Markup);
        Assert.Contains("mobile-menu-toggle", component.Markup);
        Assert.Contains("mobile-sidebar", component.Markup);
        Assert.Contains("navbar-actions", component.Markup);
    }

    [Fact]
    public void ModernLayout_MobileBrand_ClosesMenuOnClick()
    {
        // Act
        var component = RenderComponent<ModernLayout>();
        var mobileToggle = component.Find("button.mobile-menu-toggle");
        
        // Open mobile menu
        mobileToggle.Click();
        
        // Click brand in mobile sidebar
        var mobileBrand = component.FindAll("a.navbar-brand")[1]; // Second brand link (in mobile sidebar)
        mobileBrand.Click();

        // Assert mobile menu is closed
        var mobileSidebar = component.Find(".mobile-sidebar");
        Assert.DoesNotContain("open", mobileSidebar.ClassList);
    }
}