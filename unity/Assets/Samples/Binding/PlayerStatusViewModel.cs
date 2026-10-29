using System.ComponentModel;

namespace Xamlity.Samples.Binding
{
    public sealed class PlayerStatusViewModel : INotifyPropertyChanged
    {
        private PlayerModel player;
        private int gold;

        public event PropertyChangedEventHandler PropertyChanged;

        public PlayerModel Player
        {
            get => player;
            set
            {
                if (player == value) return;
                player = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Player)));
            }
        }

        public int Gold
        {
            get => gold;
            set
            {
                if (gold == value) return;
                gold = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gold)));
            }
        }
    }

    public sealed class PlayerModel : INotifyPropertyChanged
    {
        private string name;
        private int hp;
        private int maxHp;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Name
        {
            get => name;
            set
            {
                if (name == value) return;
                name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public int Hp
        {
            get => hp;
            set
            {
                if (hp == value) return;
                hp = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hp)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HpRatio)));
            }
        }

        public int MaxHp
        {
            get => maxHp;
            set
            {
                if (maxHp == value) return;
                maxHp = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxHp)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HpRatio)));
            }
        }

        public double HpRatio => maxHp > 0 ? (double)hp / maxHp : 0;
    }
}
