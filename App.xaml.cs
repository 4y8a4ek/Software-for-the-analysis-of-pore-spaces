using Microsoft.UI.Xaml;
using DigitalCoreAnalyser.Views;

namespace DigitalCoreAnalyser
{
    public partial class App : Application
    {
        private Window m_window; // Добавили ?

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            var mainPage = new MainPage();
            m_window.Content = mainPage;
            m_window.Activate();
        }
    }
}