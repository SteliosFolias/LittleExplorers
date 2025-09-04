using CommunityToolkit.Maui;

namespace LittleExplorers;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()              // γενικό Toolkit
            .UseMauiCommunityToolkitCamera()        // Camera (experimental)
            .UseMauiCommunityToolkitMediaElement()  // MediaElement
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        return builder.Build();
    }
}
