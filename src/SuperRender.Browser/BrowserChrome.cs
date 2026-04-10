using SuperRender.Core;
using SuperRender.Core.Layout;
using SuperRender.Core.Painting;

namespace SuperRender.Browser;

/// <summary>
/// Hit test result for the browser chrome area.
/// </summary>
public enum ChromeHitArea
{
    None,
    TabClicked,
    NewTabButton,
    CloseTabButton,
    AddressBar,
    BackButton,
    ForwardButton,
    ReloadButton,
    GoButton,
}

public readonly record struct ChromeHitResult(ChromeHitArea Area, int TabIndex = -1);

/// <summary>
/// Renders the browser chrome (tab bar + address bar) as paint commands.
/// </summary>
public sealed class BrowserChrome
{
    public const float TabBarHeight = 32f;
    public const float AddressBarHeight = 36f;
    public const float TotalChromeHeight = TabBarHeight + AddressBarHeight;

    private const float TabWidth = 160f;
    private const float TabPadding = 4f;
    private const float NewTabButtonWidth = 28f;
    private const float ButtonWidth = 32f;
    private const float AddressBarPadding = 8f;

    // Colors
    private static readonly Color ChromeBg = Color.FromRgb(222, 226, 230);
    private static readonly Color TabBg = Color.FromRgb(248, 249, 250);
    private static readonly Color TabActiveBg = Color.White;
    private static readonly Color TabBorder = Color.FromRgb(173, 181, 189);
    private static readonly Color AddressBarBg = Color.White;
    private static readonly Color AddressBarBorder = Color.FromRgb(173, 181, 189);
    private static readonly Color TextColor = Color.FromRgb(33, 37, 41);
    private static readonly Color ButtonBg = Color.FromRgb(233, 236, 239);
    private static readonly Color ButtonText = Color.FromRgb(73, 80, 87);

    public bool AddressBarFocused { get; set; }
    public string AddressText { get; set; } = "";
    public int CursorPosition { get; set; }

    /// <summary>
    /// Builds a PaintList for the browser chrome (tab bar + address bar).
    /// </summary>
    public PaintList BuildPaintList(float viewportWidth, TabManager tabs)
    {
        var list = new PaintList();

        // Tab bar background
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, 0, viewportWidth, TabBarHeight),
            Color = ChromeBg,
        });

        // Tabs
        float tabX = TabPadding;
        for (int i = 0; i < tabs.Tabs.Count; i++)
        {
            var tab = tabs.Tabs[i];
            var isActive = i == tabs.ActiveTabIndex;
            var bg = isActive ? TabActiveBg : TabBg;
            var tabW = Math.Min(TabWidth, (viewportWidth - NewTabButtonWidth - TabPadding * 2) / Math.Max(tabs.Tabs.Count, 1));

            // Tab background
            list.Add(new FillRectCommand
            {
                Rect = new RectF(tabX, TabPadding, tabW - 2, TabBarHeight - TabPadding),
                Color = bg,
            });

            // Tab border (bottom line if not active)
            if (!isActive)
            {
                list.Add(new FillRectCommand
                {
                    Rect = new RectF(tabX, TabBarHeight - 1, tabW - 2, 1),
                    Color = TabBorder,
                });
            }

            // Tab title text (truncated)
            var title = tab.Title;
            if (title.Length > 18) title = title[..17] + "...";
            list.Add(new DrawTextCommand
            {
                Text = title,
                X = tabX + 8,
                Y = TabPadding + 4,
                FontSize = 12,
                Color = TextColor,
            });

            // Close button "x" on active tab
            if (isActive && tabs.Tabs.Count > 1)
            {
                list.Add(new DrawTextCommand
                {
                    Text = "x",
                    X = tabX + tabW - 16,
                    Y = TabPadding + 4,
                    FontSize = 12,
                    Color = ButtonText,
                });
            }

            tabX += tabW;
        }

        // New tab button "+"
        list.Add(new FillRectCommand
        {
            Rect = new RectF(tabX + 2, TabPadding, NewTabButtonWidth, TabBarHeight - TabPadding * 2),
            Color = ButtonBg,
        });
        list.Add(new DrawTextCommand
        {
            Text = "+",
            X = tabX + 10,
            Y = TabPadding + 3,
            FontSize = 14,
            Color = ButtonText,
        });

        // Bottom line of tab bar
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, TabBarHeight - 1, viewportWidth, 1),
            Color = TabBorder,
        });

        // Address bar background
        float addrY = TabBarHeight;
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, addrY, viewportWidth, AddressBarHeight),
            Color = ChromeBg,
        });

        // Navigation buttons
        float btnY = addrY + 4;
        float btnH = AddressBarHeight - 8;
        float btnX = AddressBarPadding;

        // Back button
        DrawButton(list, btnX, btnY, ButtonWidth, btnH, "<");
        btnX += ButtonWidth + 4;

        // Forward button
        DrawButton(list, btnX, btnY, ButtonWidth, btnH, ">");
        btnX += ButtonWidth + 4;

        // Reload button
        DrawButton(list, btnX, btnY, ButtonWidth, btnH, "R");
        btnX += ButtonWidth + 8;

        // Address bar input
        float addrRight = viewportWidth - AddressBarPadding - ButtonWidth - 4;
        float addrWidth = addrRight - btnX;

        // Address bar box
        list.Add(new FillRectCommand
        {
            Rect = new RectF(btnX, btnY, addrWidth, btnH),
            Color = AddressBarBg,
        });
        list.Add(new StrokeRectCommand
        {
            Rect = new RectF(btnX, btnY, addrWidth, btnH),
            Color = AddressBarFocused ? Color.FromRgb(0, 123, 255) : AddressBarBorder,
            LineWidth = AddressBarFocused ? 2f : 1f,
        });

        // Address text
        var displayText = AddressText;
        if (displayText.Length > 80) displayText = displayText[..80] + "...";
        list.Add(new DrawTextCommand
        {
            Text = displayText,
            X = btnX + 6,
            Y = btnY + 4,
            FontSize = 13,
            Color = TextColor,
        });

        // Cursor (if focused)
        if (AddressBarFocused)
        {
            // Simple cursor: a thin line after text
            float cursorX = btnX + 6 + CursorPosition * 7.8f; // approximate char width
            list.Add(new FillRectCommand
            {
                Rect = new RectF(cursorX, btnY + 3, 1.5f, btnH - 6),
                Color = TextColor,
            });
        }

        // Go button
        float goBtnX = addrRight + 4;
        DrawButton(list, goBtnX, btnY, ButtonWidth, btnH, "Go");

        // Bottom border of address bar
        list.Add(new FillRectCommand
        {
            Rect = new RectF(0, addrY + AddressBarHeight - 1, viewportWidth, 1),
            Color = TabBorder,
        });

        return list;
    }

    private static void DrawButton(PaintList list, float x, float y, float w, float h, string label)
    {
        list.Add(new FillRectCommand
        {
            Rect = new RectF(x, y, w, h),
            Color = ButtonBg,
        });
        list.Add(new StrokeRectCommand
        {
            Rect = new RectF(x, y, w, h),
            Color = Color.FromRgb(206, 212, 218),
            LineWidth = 1f,
        });
        list.Add(new DrawTextCommand
        {
            Text = label,
            X = x + 6,
            Y = y + 4,
            FontSize = 13,
            Color = ButtonText,
        });
    }

    /// <summary>
    /// Hit-test a mouse click against the chrome area.
    /// </summary>
    public static ChromeHitResult HitTest(float x, float y, float viewportWidth, TabManager tabs)
    {
        // Tab bar area
        if (y < TabBarHeight)
        {
            float tabX = TabPadding;
            for (int i = 0; i < tabs.Tabs.Count; i++)
            {
                var tabW = Math.Min(TabWidth, (viewportWidth - NewTabButtonWidth - TabPadding * 2) / Math.Max(tabs.Tabs.Count, 1));

                if (x >= tabX && x < tabX + tabW)
                {
                    // Check close button on active tab
                    if (i == tabs.ActiveTabIndex && tabs.Tabs.Count > 1 && x > tabX + tabW - 20)
                        return new ChromeHitResult(ChromeHitArea.CloseTabButton, i);
                    return new ChromeHitResult(ChromeHitArea.TabClicked, i);
                }
                tabX += tabW;
            }

            if (x >= tabX && x < tabX + NewTabButtonWidth + 4)
                return new ChromeHitResult(ChromeHitArea.NewTabButton);

            return new ChromeHitResult(ChromeHitArea.None);
        }

        // Address bar area
        if (y < TotalChromeHeight)
        {
            float btnX = AddressBarPadding;

            // Back
            if (x >= btnX && x < btnX + ButtonWidth)
                return new ChromeHitResult(ChromeHitArea.BackButton);
            btnX += ButtonWidth + 4;

            // Forward
            if (x >= btnX && x < btnX + ButtonWidth)
                return new ChromeHitResult(ChromeHitArea.ForwardButton);
            btnX += ButtonWidth + 4;

            // Reload
            if (x >= btnX && x < btnX + ButtonWidth)
                return new ChromeHitResult(ChromeHitArea.ReloadButton);
            btnX += ButtonWidth + 8;

            // Go button
            float goBtnX = viewportWidth - AddressBarPadding - ButtonWidth;
            if (x >= goBtnX)
                return new ChromeHitResult(ChromeHitArea.GoButton);

            // Address bar
            if (x >= btnX && x < goBtnX)
                return new ChromeHitResult(ChromeHitArea.AddressBar);
        }

        return new ChromeHitResult(ChromeHitArea.None);
    }
}
