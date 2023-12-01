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
using System.Drawing;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MSDF.MonoGame
{
    public class StringMesh
    {
        private VertexPositionTexture[] _vertices;
        private short[] _indices;

        private int _lastHash = 0;
        private int _lastSize = 0;

        public StringMesh()
        {
            
        }
        
        public StringMesh(TextDrawRecipe recipe, ScalableFont font)
        {
            Rebuild(recipe, font);
        }

        public void Rebuild(TextDrawRecipe recipe, ScalableFont font)
        {
            int hash = recipe.GetHashCode();
            int size = recipe.Text.Length;

            bool mustBuild = _lastHash != hash;

            if (_lastSize != size)
            {
                Allocate(recipe);
                mustBuild = true;
            }

            if (mustBuild)
                Build(recipe, font);
            
            _lastSize = size;
            _lastHash = hash;
        }

        private void Allocate(TextDrawRecipe recipe)
        {
            int length = recipe.Text.Length - recipe.Text.Count(c => char.IsWhiteSpace(c));

            _vertices = new VertexPositionTexture[length * 4];
            _indices = new short[length * 6];
        }

        private void Build(TextDrawRecipe recipe, ScalableFont font)
        {
            RectangleF stringBounds = font.MeasureString(recipe.Text);

            Vector2 offset = new Vector2(stringBounds.X, -stringBounds.Y);
            offset += new Vector2(stringBounds.Width * -recipe.HorizontalAlign,
                stringBounds.Height * -recipe.VerticalAlign);
            offset *= recipe.PixelSize;

            Vector2 shift = offset;
            uint vertIndex = 0;
            uint triIndex = 0;

            foreach (char c in recipe.Text)
            {
                if (c == '\n')
                {
                    shift.Y += recipe.LineSpacing * font.Metrics.lineHeight * recipe.PixelSize;
                    shift.X = offset.X;
                    continue;
                }

                if (!font.GlyphTable.TryGetValue(c, out CachedGlyph glyph))
                    continue;

                if (glyph.DrawRectangle.HasValue)
                {
                    var bounds = glyph.AtlasBounds;

                    var plane = glyph.DrawRectangle.Value;

                    Vector2 v1 = shift + (new Vector2(plane.Left, plane.Top) * recipe.PixelSize);
                    Vector2 v2 = shift + (new Vector2(plane.Right, plane.Bottom) * recipe.PixelSize);

                    _vertices[vertIndex].Position = new Vector3(v1.X, v1.Y, 0.0F);
                    _vertices[vertIndex + 1].Position = new Vector3(v2.X, v1.Y, 0.0F);
                    _vertices[vertIndex + 2].Position = new Vector3(v1.X, v2.Y, 0.0F);
                    _vertices[vertIndex + 3].Position = new Vector3(v2.X, v2.Y, 0.0F);

                    _vertices[vertIndex].TextureCoordinate = new Vector2(bounds.left, bounds.top);
                    _vertices[vertIndex + 1].TextureCoordinate = new Vector2(bounds.right, bounds.top);
                    _vertices[vertIndex + 2].TextureCoordinate = new Vector2(bounds.left, bounds.bottom);
                    _vertices[vertIndex + 3].TextureCoordinate = new Vector2(bounds.right, bounds.bottom);

                    _indices[triIndex] = (short)vertIndex;
                    _indices[triIndex + 1] = (short)(vertIndex + 1);
                    _indices[triIndex + 2] = (short)(vertIndex + 2);
                    _indices[triIndex + 3] = (short)(vertIndex + 3);
                    _indices[triIndex + 4] = (short)(vertIndex + 2);
                    _indices[triIndex + 5] = (short)(vertIndex + 1);

                    vertIndex += 4;
                    triIndex += 6;
                }

                shift.X += glyph.Advance * recipe.PixelSize * (recipe.RightToLeft ? -1F : 1F) * recipe.CharacterSpacing;
            }
        }

        public void Draw(ref GraphicsDevice graphicsDevice)
        {
            graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _vertices,
                0,
                _vertices.Length,
                _indices,
                0,
                _indices.Length / 3);
        }
    }
}