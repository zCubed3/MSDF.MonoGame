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
using System.Drawing;
using System.Linq;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Newtonsoft.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace MSDF.MonoGame
{
    /// <summary>
    /// Collection of settings used to define a MSDF draw-call
    /// </summary>
    public class TextDrawRecipe
    {
        public float PixelSize = 64;
        public Matrix ModelMatrix = Matrix.Identity;
        public Vector4 Color = Microsoft.Xna.Framework.Color.White.ToVector4();

        public float HorizontalAlign = 0F;
        public float VerticalAlign = 0F;
        public float CharacterSpacing = 1.0F;
        public float LineSpaceScale = 1.0F;
        
        public bool RightToLeft = false;

        public string Text = null;
    }
    
    /// <summary>
    /// JSON serialized MSDF atlas data
    /// </summary>
    public class MSDFAtlas
    {
        public string type;
        public float distanceRange;
        public float size;
        public int width;
        public int height;
        public string yOrigin;
    }

    /// <summary>
    /// JSON serialized MSDF freetype metric data
    /// </summary>
    public class MSDFMetrics
    {
        public float emSize;
        public float lineHeight;
        public float ascender;
        public float descender;
        public float underlineY;
        public float underlineThickness;
    }
    
    /// <summary>
    /// JSON serialized glyph bounds
    /// </summary>
    public class MSDFGlyphBounds
    {
        public float left;
        public float bottom;
        public float right;
        public float top;
    }

    /// <summary>
    /// JSON serialized glyph data
    /// </summary>
    public class MSDFGlyph
    {
        public char unicode;
        public float advance;
        public MSDFGlyphBounds planeBounds;
        public MSDFGlyphBounds atlasBounds;
    }

    /// <summary>
    /// JSON serialized MSDF schema
    /// </summary>
    public class MSDFSchema
    {
        [JsonProperty("atlas")]
        public MSDFAtlas Atlas;
        
        [JsonProperty("metrics")]
        public MSDFMetrics Metrics;
        
        [JsonProperty("glyphs")]
        public MSDFGlyph[] Glyphs;
    }
    
    /// <summary>
    /// Simplified in-memory glyph data representation
    /// </summary>
    public class CachedGlyph
    {
        public float Advance = 0.0F;
        public MSDFGlyphBounds AtlasBounds;
        public RectangleF? DrawRectangle;

        public CachedGlyph(MSDFGlyph glyph)
        {
            this.Advance = glyph.advance;
            this.AtlasBounds = glyph.atlasBounds;

            if (glyph.planeBounds != null)
            {
                this.DrawRectangle = RectangleF.FromLTRB(
                    glyph.planeBounds.left,
                    glyph.planeBounds.top,
                    glyph.planeBounds.right,
                    glyph.planeBounds.bottom
                );
            }
            else
                this.DrawRectangle = null;
        }
    }
    
    /// <summary>
    /// MSDF based alternative to MonoGame's built in "SpriteFont"
    /// </summary>
    public class ScalableFont
    {
        //
        // Statics
        //
        public static Effect MSDFShader { get; private set; }
        
        public static Dictionary<string, ScalableFont> LoadedFonts = new Dictionary<string, ScalableFont>();
        
        public static DrawingLayer FontDrawingLayer = new();
        public static RasterizerState RasterizerState = new() { CullMode = CullMode.None };

        //
        // Members
        //
        public MSDFAtlas Atlas { get; private set; }
        
        public MSDFMetrics Metrics { get; private set; }

        public Texture2D AtlasTexture { get; private set; } = null;
        
        
        private MSDFGlyph[] _uncachedGlyphs = null;
        private List<TextDrawRecipe> _drawRecipes = new();

        public Dictionary<char, CachedGlyph> GlyphTable { get; private set; }
        public string FontName { get; private set; }

        public static ScalableFont Load(Game game, string fontName, string folder = "Fonts/")
        {
            if (LoadedFonts.TryGetValue(fontName, out ScalableFont cached))
                return cached;

            string atlasPath = $"{folder}{fontName}.png";
            string schemaPath = $"{folder}{fontName}.json";

            if (!File.Exists(atlasPath))
                throw new FileNotFoundException("Atlas texture doesn't exist!");

            if (!File.Exists(schemaPath))
                throw new FileNotFoundException("Json schema doesn't exist!");

            string jsonSchema = File.ReadAllText(schemaPath);
            MSDFSchema schema = JsonConvert.DeserializeObject<MSDFSchema>(jsonSchema);

            ScalableFont font = new ScalableFont();

            font.Atlas = schema.Atlas;
            font.Metrics = schema.Metrics;
            font._uncachedGlyphs = schema.Glyphs;
            
            font.FontName = fontName;
            font.AtlasTexture = Texture2D.FromFile(game.GraphicsDevice, atlasPath);
            
            LoadedFonts[fontName] = font;
            font.CacheGlyphs();

            return font;
        }

        private void CacheGlyphs()
        {
            if (_uncachedGlyphs == null)
                return;

            GlyphTable = new Dictionary<char, CachedGlyph>();
            
            foreach (var glyph in _uncachedGlyphs)
            {
                GlyphTable.Add(glyph.unicode, new CachedGlyph(glyph));
            }
            
            _uncachedGlyphs = null;
        }

        /// <summary>
        /// Attempts to guess a best fit for a bounding box around the text
        /// </summary>
        /// <param name="text">String to measure</param>
        /// <param name="lineSpacing">Spacing between lines</param>
        /// <param name="characterSpacing">Whitespace multipler</param>
        /// <returns></returns>
        public RectangleF MeasureString(string text, float lineSpacing = 1.0F, float characterSpacing = 1.0F)
        {
            if (string.IsNullOrEmpty(text))
                return RectangleF.Empty;

            RectangleF rect = RectangleF.Empty;
            RectangleF testRect = RectangleF.Empty;
            
            float carriage = 0.0F;
            float line = 0.0F;
            
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    carriage = 0.0F;
                    line += lineSpacing * Metrics.lineHeight;
                    
                    continue;
                }
                
                if (GlyphTable.TryGetValue(c, out CachedGlyph glyph))
                {
                    if (glyph.DrawRectangle.HasValue)
                    {
                        testRect = glyph.DrawRectangle.Value;

                        testRect.X += carriage;
                        testRect.Y += line;

                        rect = RectangleF.Union(testRect, rect);
                    }

                    carriage += glyph.Advance * characterSpacing;
                }
            }

            return rect;
        }

        public enum HAlignment
        {
            Left, Center, Right
        }

        public enum VAlignment
        {
            Top, Middle, Bottom
        }

        public static float GetHAlignWeight(HAlignment alignment)
        {
            switch (alignment)
            {
                default:
                    return 0;

                case HAlignment.Center:
                    return 0.5F;

                case HAlignment.Right:
                    return 1F;
            }
        }

        public static float GetVAlignWeight(VAlignment alignment)
        {
            switch (alignment)
            {
                default:
                    return 0;

                case VAlignment.Middle:
                    return 0.5F;

                case VAlignment.Bottom:
                    return 1F;
            }
        }

        public void Draw(TextDrawRecipe recipe)
        {
            _drawRecipes.Add(recipe);
        }
        
        public void Draw(string text, Matrix matrix, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
        {
            TextDrawRecipe recipe = new TextDrawRecipe()
            {
                PixelSize = pixelSize,
                ModelMatrix = matrix,
                Color = color.ToVector4(),
                HorizontalAlign = halign,
                VerticalAlign = valign,
                RightToLeft = rtl,
                Text = text
            };

            Draw(recipe);
        }

        public void Draw(string text, Vector3 position, Quaternion rotation, Vector2 scale, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
        {
            Matrix mat = Matrix.CreateFromQuaternion(rotation)
                    * Matrix.CreateScale(new Vector3(scale, 1))
                    * Matrix.CreateTranslation(position);

            Draw(text, mat, halign, valign, pixelSize, color, rtl);
        }

        public void Draw(string text, Vector2 position, float depth, float angle, Vector2 scale, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
            => Draw(text, new Vector3(position, depth - 1F), Quaternion.CreateFromAxisAngle(Vector3.Forward, angle), scale, halign, valign, pixelSize, color);

        public void Draw(string text, Vector2 position, float depth, float angle, Vector2 scale, HAlignment halign = HAlignment.Left, VAlignment valign = VAlignment.Top, float pixelSize = 64, Color color = default, bool rtl = false)
            => Draw(text, position, depth, angle, scale, GetHAlignWeight(halign), GetVAlignWeight(valign), pixelSize, color, rtl);

        public void Draw(string text, Vector2 position, float depth, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
            => Draw(text, position, depth, 0F, Vector2.One, halign, valign, pixelSize, color, rtl);

        public void Draw(string text, Vector2 position, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
            => Draw(text, position, 0F, 0F, Vector2.One, halign, valign, pixelSize, color, rtl);

        private void DrawRecipe(ref GraphicsDevice graphicsDevice, TextDrawRecipe recipe)
        {
            if (string.IsNullOrEmpty(recipe.Text))
                return;
            
            string technique = recipe.PixelSize > 32 ? "LargeText" : "SmallText";

            // TODO: Does this severely effect performance?
            MSDFShader.Parameters["WorldMatrix"].SetValue(recipe.ModelMatrix);
            MSDFShader.Parameters["ForegroundColor"].SetValue(recipe.Color);
            MSDFShader.CurrentTechnique = MSDFShader.Techniques[technique];
            MSDFShader.CurrentTechnique.Passes[0].Apply();
                
            RectangleF bounds = MeasureString(recipe.Text);
                
            Vector2 offset = new Vector2(bounds.X, -bounds.Y);
            offset += new Vector2(bounds.Width * -recipe.HorizontalAlign, bounds.Height * -recipe.VerticalAlign);
            offset *= recipe.PixelSize;
            
            StringMesh mesh = new StringMesh(recipe, this, offset);
            mesh.Draw(ref graphicsDevice);
        }

        public static void RenderAll(Game game, SpriteBatch batch, Matrix? matrix = null)
        {
            Matrix drawMatrix = matrix ?? Matrix.Identity;
            drawMatrix *= Matrix.CreateScale(1.0F, -1.0F, 1.0F);
            
            if (MSDFShader == null)
                MSDFShader = game.Content.Load<Effect>("FieldFontEffect");

            foreach (var font in LoadedFonts.Values)
                font.Render(game, batch, drawMatrix);
        }

        public void Render(Game game, SpriteBatch batch, Matrix viewProjMatrix)
        {
            MSDFShader.Parameters["TextureSize"]?.SetValue(AtlasTexture.GetSize());
            MSDFShader.Parameters["PxRange"]?.SetValue(Atlas.distanceRange);
            MSDFShader.Parameters["GlyphTexture"]?.SetValue(AtlasTexture);
            MSDFShader.Parameters["ViewProjectionMatrix"].SetValue(viewProjMatrix);
            MSDFShader.CurrentTechnique.Passes[0].Apply();

            var previousState = game.GraphicsDevice.RasterizerState;
            game.GraphicsDevice.RasterizerState = RasterizerState;

            FontDrawingLayer.Matrix = viewProjMatrix;
            
            FontDrawingLayer.Begin(batch);

            GraphicsDevice graphicsDevice = game.GraphicsDevice;
            
            foreach (TextDrawRecipe recipe in _drawRecipes)
                DrawRecipe(ref graphicsDevice, recipe);
            
            FontDrawingLayer.End(batch);

            Flush();
            
            game.GraphicsDevice.RasterizerState = previousState;
        }

        /// <summary>
        /// Flushes the draw queue, ensuring that vsync doesn't cause duplicates
        /// </summary>
        public void Flush()
        {
            _drawRecipes.Clear();
        }
    }
}
