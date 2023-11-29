using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MSDF.MonoGame
{
    public class DrawingLayer
    {
        public SpriteSortMode SortMode = SpriteSortMode.BackToFront;
        public BlendState BlendState = BlendState.AlphaBlend;
        public SamplerState SamplerState = SamplerState.LinearWrap;
        public Matrix? Matrix = null;
        public Effect Effect = null;

        public SpriteBatch Batch;

        public static DrawingLayer ActiveLayer;

        public DrawingLayer() { }

        public DrawingLayer(SpriteSortMode sortMode, BlendState blendState, SamplerState samplerState, Matrix? matrix, Effect effect)
        {
            this.SortMode = sortMode;
            this.BlendState = blendState;
            this.SamplerState = samplerState;
            this.Matrix = matrix;
            this.Effect = effect;
        }

        public void Begin(SpriteBatch batch)
        {
            this.Batch = batch;
            ActiveLayer = this;

            batch.Begin(SortMode, BlendState, SamplerState, DepthStencilState.Default, null, Effect, Matrix);
        }

        public void End(SpriteBatch batch)
        {
            this.Batch = null;
            ActiveLayer = null;

            batch.End();
        }

        public ScopedLayer BeginScoped(SpriteBatch batch, Matrix? matrix = null, Effect effect = null)
        {
            return new ScopedLayer(batch, this, matrix, effect);
        }
    }

    public class ScopedLayer : IDisposable
    {
        protected SpriteBatch batch;
        protected DrawingLayer layer;

        public ScopedLayer(SpriteBatch batch, DrawingLayer layer, Matrix? matrix = null, Effect effect = null)
        {
            layer.Matrix = matrix;
            layer.Effect = effect;
            layer.Begin(batch);

            this.layer = layer;
            this.batch = batch;
        }

        public ScopedLayer(SpriteBatch batch, DrawingLayer layer) : this(batch, layer, null) { }

        public void Dispose()
        {
            layer.End(batch);
        }
    }
}
