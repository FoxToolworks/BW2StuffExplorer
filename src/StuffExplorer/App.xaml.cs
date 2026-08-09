using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace StuffExplorer;

public partial class App : Application
{
    public App()
    {
        // Keep the English UI text separate from the user's Windows regional
        // settings. WPF otherwise formats bindings with en-US defaults.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));
    }
}
