using TMPro;
using UnityEngine;

namespace Xamlity.Unity
{
    public sealed class XamlitySettings : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset defaultFont;
        [SerializeField] private XamlAsset applicationResources;

        private static XamlitySettings instance;
        private static bool loaded;

        public TMP_FontAsset DefaultFont => defaultFont;
        public XamlAsset ApplicationResources => applicationResources;

        internal static XamlitySettings Load()
        {
            if (!loaded)
            {
                instance = Resources.Load<XamlitySettings>("XamlitySettings");
                loaded = true;
                if (instance == null || instance.DefaultFont == null)
                    Debug.LogWarning("XamlitySettings or its default font is missing. Text will not be rendered.");
            }
            return instance;
        }
    }
}
