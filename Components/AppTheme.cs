using MudBlazor;

namespace CartStack.Components;

public static class AppTheme
{
    private static readonly string[] InterStack =
    [
        "Inter",
        "ui-sans-serif",
        "system-ui",
        "-apple-system",
        "Segoe UI",
        "Roboto",
        "Helvetica Neue",
        "Arial",
        "sans-serif",
    ];

    public static MudTheme Default { get; } = new()
    {
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = InterStack },
            H1 = new H1Typography { FontFamily = InterStack },
            H2 = new H2Typography { FontFamily = InterStack },
            H3 = new H3Typography { FontFamily = InterStack },
            H4 = new H4Typography { FontFamily = InterStack },
            H5 = new H5Typography { FontFamily = InterStack },
            H6 = new H6Typography { FontFamily = InterStack },
            Subtitle1 = new Subtitle1Typography { FontFamily = InterStack },
            Subtitle2 = new Subtitle2Typography { FontFamily = InterStack },
            Body1 = new Body1Typography { FontFamily = InterStack },
            Body2 = new Body2Typography { FontFamily = InterStack },
            Button = new ButtonTypography { FontFamily = InterStack },
            Caption = new CaptionTypography { FontFamily = InterStack },
            Overline = new OverlineTypography { FontFamily = InterStack },
        },
    };
}
