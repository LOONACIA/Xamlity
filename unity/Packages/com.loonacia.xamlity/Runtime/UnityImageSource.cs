using System;
using UnityEngine;
using Xamlity.Rendering;

namespace Xamlity.Unity
{
    public sealed class UnityImageSource : ImageSource
    {
        public UnityImageSource(Texture2D texture)
        {
            Texture = texture != null ? texture : throw new ArgumentNullException(nameof(texture));
        }

        public UnityImageSource(Sprite sprite)
        {
            Sprite = sprite != null ? sprite : throw new ArgumentNullException(nameof(sprite));
            Texture = sprite.texture;
        }

        public Texture2D Texture { get; }
        public Sprite Sprite { get; }
        public override double Width => Sprite != null ? Sprite.rect.width : Texture.width;
        public override double Height => Sprite != null ? Sprite.rect.height : Texture.height;
    }
}
