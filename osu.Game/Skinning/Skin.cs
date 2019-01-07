﻿// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu/master/LICENCE

using System;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Skinning
{
    public abstract class Skin : IDisposable, ISkinSource
    {
        public readonly SkinInfo SkinInfo;

        public virtual SkinConfiguration Configuration { get; protected set; }

        public event Action SourceChanged;

        public abstract Drawable GetDrawableComponent(string componentName);

        public abstract SampleChannel GetSample(string sampleName);

        public abstract Texture GetTexture(string componentName);

        public TValue GetValue<TConfiguration, TValue>(Func<TConfiguration, TValue> query) where TConfiguration : SkinConfiguration
            => Configuration is TConfiguration conf ? query.Invoke(conf) : default;

        public bool TryGetValue<TConfiguration, TValue>(Func<TConfiguration, TValue, bool> query, out TValue val) where TConfiguration : SkinConfiguration
        {
            val = default;

            if (Configuration is TConfiguration conf)
                return query(conf, val);

            return false;
        }

        protected Skin(SkinInfo skin)
        {
            SkinInfo = skin;
        }

        #region Disposal

        ~Skin()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool isDisposed;

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposed)
                return;
            isDisposed = true;
        }

        #endregion
    }
}
