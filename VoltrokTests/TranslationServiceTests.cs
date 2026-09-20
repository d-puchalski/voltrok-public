using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using VoltrokServices.Services;
using VoltrokServices.Services.Translations;

namespace VoltrokTests;

public class TranslationServiceTests
{
    [Theory]
    [InlineData("en", "Language", "Notifications")]
    [InlineData("de", "Sprache", "Benachrichtigungen")]
    [InlineData("es", "Idioma", "Notificaciones")]
    [InlineData("fr", "Langue", "Notifications")]
    [InlineData("it", "Lingua", "Notifiche")]
    [InlineData("pt", "Idioma", "Notificações")]
    [InlineData("ru", "Язык", "Уведомления")]
    [InlineData("zh", "语言", "通知")]
    public void SetLanguage_LoadsExpectedTranslations(string languageCode, string expectedLanguageLabel, string expectedNotificationsLabel)
    {
        var service = CreateTranslationService();

        var changed = service.SetLanguage(languageCode);

        Assert.True(changed);
        Assert.Equal(languageCode, service.CurrentLanguage);
        Assert.Equal(expectedLanguageLabel, service["parts.topbar.language"]);
        Assert.Equal(expectedNotificationsLabel, service["parts.notifications.title"]);
    }

    [Fact]
    public void SetLanguage_LoadsEnglishSpecificOverrides()
    {
        var service = CreateTranslationService();

        service.SetLanguage("en");

        Assert.Equal("and", service["tabs.common.and"]);
        Assert.Equal("Cheese", service["products.code.cheese"]);
    }

    [Fact]
    public void SetLanguage_FallsBackToPolish_WhenLanguageFileDoesNotExist()
    {
        var service = CreateTranslationService();

        service.SetLanguage("xx");

        Assert.Equal("xx", service.CurrentLanguage);
        Assert.Equal("Notifications", service["parts.notifications.title"]);
    }

    [Fact]
    public void SetLanguage_UsesPrimaryLanguageSubtag()
    {
        var service = CreateTranslationService();

        service.SetLanguage("de-DE");

        Assert.Equal("de", service.CurrentLanguage);
        Assert.Equal("Sprache", service["parts.topbar.language"]);
    }

    [Fact]
    public void AppState_SetLanguage_UpdatesSelectedLanguage_AndRaisesChange()
    {
        var appState = new AppState();
        var changeCount = 0;
        appState.OnChange += () => changeCount++;

        appState.SetLanguage("de-DE");

        Assert.Equal("de", appState.SelectedLanguage);
        Assert.Equal(1, changeCount);
    }

    private static TranslationService CreateTranslationService()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "webServer"));
        return new TranslationService(new TestWebHostEnvironment(contentRoot));
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "webServer.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(Path.Combine(contentRootPath, "wwwroot"));
        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRootPath);
    }
}
