using System.ComponentModel;

namespace Xamlity.Samples.Styles
{
    public sealed class StyleShowcaseViewModel : INotifyPropertyChanged
    {
        private string status = "정상";

        public event PropertyChangedEventHandler PropertyChanged;

        public string Status
        {
            get => status;
            set
            {
                if (status == value) return;
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }
}
