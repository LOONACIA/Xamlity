using UnityEngine;
using Xamlity.Unity;

namespace Xamlity.Samples.Styles
{
    public sealed class StyleShowcaseDemo : MonoBehaviour
    {
        [SerializeField] private XamlityRoot root;

        private StyleShowcaseViewModel viewModel;
        private float elapsed;

        private void Start()
        {
            viewModel = new StyleShowcaseViewModel();
            root.DataContext = viewModel;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed < 3f) return;
            elapsed -= 3f;

            viewModel.Status = viewModel.Status == "정상" ? "위험" : "정상";
        }
    }
}
