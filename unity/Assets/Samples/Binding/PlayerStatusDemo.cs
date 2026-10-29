using UnityEngine;
using Xamlity.Unity;

namespace Xamlity.Samples.Binding
{
    public sealed class PlayerStatusDemo : MonoBehaviour
    {
        [SerializeField] private XamlityRoot root;

        private PlayerStatusViewModel viewModel;
        private float elapsed;

        private void Start()
        {
            viewModel = new PlayerStatusViewModel
            {
                Player = new PlayerModel { Name = "별빛 탐험가", Hp = 150, MaxHp = 150 },
                Gold = 1200
            };
            root.DataContext = viewModel;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < 1f) return;
            elapsed -= 1f;

            var player = viewModel.Player;
            player.Hp = player.Hp == 0 ? player.MaxHp : Mathf.Max(0, player.Hp - 15);
            viewModel.Gold += 10;
        }
    }
}
