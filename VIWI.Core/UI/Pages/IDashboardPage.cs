namespace VIWI.UI.Pages
{
    // It might be better to move module descriptions over here later idk.

    public interface IDashboardPage
    {
        string DisplayName { get; }
        string Version { get; }
        string Category { get; }
        void Draw();
        bool SupportsEnableToggle { get; }
        bool IsEnabled { get; }
        void SetEnabled(bool value);
        bool RequiresUnlock => false;
    }
}
