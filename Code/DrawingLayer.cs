/*
 * Copyright (c) 2023 Liam R. (zCubed3)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

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
