using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Newtonsoft.Json;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MSDF.MonoGame
{
    /// <summary>
    /// MSDF based alternative to MonoGame's built in "SpriteFont"
    /// </summary>
    public class ScalableFont
    {
        // TODO: Cache this?
        public class StringMesh
        {
            private VertexPositionTexture[] _vertices;
            private readonly short[] _indices;
            private int _length = 0; 

            public StringMesh(string str, ScalableFont font, Vector2 origin, MSDFDrawSettings drawSettings, float whiteSpaceWidth)
            {
                _length = str.Length - str.Count(c => char.IsWhiteSpace(c));
                
                _vertices = new VertexPositionTexture[_length * 4];
                _indices = new short[_length * 6];

                Vector2 shift = origin;
                uint vertIndex = 0;
                uint triIndex = 0;
                
                foreach (char c in str)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        shift.X += whiteSpaceWidth * drawSettings.pixelSize;
                        continue;
                    }
                    
                    if (!font.GlyphTable.TryGetValue(c, out MSDFGlyph glyph))
                        continue;
                    
                    if (glyph == null)
                        continue;

                    // TODO: Better drawing parameters?
                    var bounds = glyph.atlasBounds;
                    
                    var plane = glyph.planeBounds;

                    Vector2 v1 = shift + (new Vector2(plane.left, plane.top) * drawSettings.pixelSize);
                    Vector2 v2 = shift + (new Vector2(plane.right, plane.bottom) * drawSettings.pixelSize);
                    
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
                    
                    shift.X += glyph.advance * drawSettings.pixelSize * (drawSettings.rtl ? -1F : 1F);
                }
            }
            
            public void Draw(GraphicsDevice graphicsDevice)
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
        
        public class GlyphQuad
        {
            public readonly VertexPositionTexture[] vertices;
            public readonly short[] indices;

            public GlyphQuad()
            {
                vertices = new[]
                {
                    new VertexPositionTexture(
                        new Vector3(0, 0, 0),
                        new Vector2(1, 1)),
                    new VertexPositionTexture(
                        new Vector3(0, 0, 0),
                        new Vector2(0, 1)),
                    new VertexPositionTexture(
                        new Vector3(0, 0, 0),
                        new Vector2(0, 0)),
                    new VertexPositionTexture(
                        new Vector3(0, 0, 0),
                        new Vector2(1, 0))
                };

                indices = new short[] { 0, 1, 2, 2, 3, 0 };
            }


            public void Render(GraphicsDevice device, Vector2 v1, Vector2 v2)
            {
                vertices[0].Position.X = v2.X;
                vertices[0].Position.Y = v1.Y;

                vertices[1].Position.X = v1.X;
                vertices[1].Position.Y = v1.Y;

                vertices[2].Position.X = v1.X;
                vertices[2].Position.Y = v2.Y;

                vertices[3].Position.X = v2.X;
                vertices[3].Position.Y = v2.Y;

                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    vertices,
                    0,
                    4,
                    indices,
                    0,
                    2);
            }
        }

        public static Game ActiveGame = null;
        public static Dictionary<string, ScalableFont> LoadedFonts = new Dictionary<string, ScalableFont>();
        public static Effect MSDFShader = null;
        public static DrawingLayer FontDrawingLayer = new DrawingLayer();
        public static GlyphQuad Quad = new GlyphQuad();
        public static RasterizerState RasterizerState = new RasterizerState() { CullMode = CullMode.None };
        
        public class MSDFAtlas
        {
            public string type;
            public float distanceRange;
            public float size;
            public int width;
            public int height;
            public string yOrigin;
        }

        public class MSDFMetrics
        {
            public float emSize;
            public float lineHeight;
            public float ascender;
            public float descender;
            public float underlineY;
            public float underlineThickness;
        }

        public class MSDFGlyphBounds
        {
            public float left;
            public float bottom;
            public float right;
            public float top;
        }

        public class MSDFGlyph
        {
            public char unicode;
            public float advance;
            public MSDFGlyphBounds planeBounds;
            public MSDFGlyphBounds atlasBounds;

            public float Width = 0;
            public float Height = 0;

            public float Bottom = 0;
            public float Top = 0;

            public void CalculateLayout(MSDFAtlas atlas)
            {
                Width = (atlasBounds.right - atlasBounds.left) / atlas.size;
                Height = (atlasBounds.bottom - atlasBounds.top) / atlas.size;
                Bottom = planeBounds.bottom;
                Top = planeBounds.top;
            }
        }


        public MSDFAtlas atlas;
        public MSDFMetrics metrics;
        public MSDFGlyph[] glyphs;

        public float whiteSpaceMultiplier = 1.0F;
        public float WhiteSpaceWidth => _whiteSpaceBaseSize * whiteSpaceMultiplier;

        private float _whiteSpaceBaseSize = 0.25F;

        public float LineHeight { get; private set; } = 1.0F;
        
        public Dictionary<char, MSDFGlyph> GlyphTable { get; private set; } = new Dictionary<char, MSDFGlyph>();
        public string FontName { get; private set; }

        [NonSerialized]
        public Texture2D atlasTexture;


        public static ScalableFont Load(Game game, string fontName, string folder = "Fonts/")
        {
            if (ActiveGame == null)
                ActiveGame = game;

            if (LoadedFonts.ContainsKey(fontName))
                return LoadedFonts[fontName];

            string atlasPath = $"{folder}{fontName}.png";
            string schemaPath = $"{folder}{fontName}.json";

            if (!File.Exists(atlasPath))
                throw new FileNotFoundException("Atlas texture doesn't exist!");

            if (!File.Exists(schemaPath))
                throw new FileNotFoundException("Json schema doesn't exist!");

            string jsonSchema = File.ReadAllText(schemaPath);
            ScalableFont font = JsonConvert.DeserializeObject<ScalableFont>(jsonSchema);

            font.atlasTexture = Texture2D.FromFile(ActiveGame.GraphicsDevice, atlasPath);
            LoadedFonts[fontName] = font;

            font.FontName = fontName;
            font.CacheGlyphs();

            return font;
        }

        protected void CacheGlyphs()
        {
            if (glyphs == null)
                return;

            foreach (var glyph in glyphs)
            {
                if (glyph.atlasBounds != null && glyph.planeBounds != null) 
                    glyph.CalculateLayout(atlas);
                
                GlyphTable.Add(glyph.unicode, glyph);

                if (glyph.unicode == ' ')
                    _whiteSpaceBaseSize = glyph.advance;
            }

            glyphs = null;
        }


        // MSDF font drawing is deferred!
        // Make sure to call "MSDFFont.RenderAll()" to render things in the local context!
        public class MSDFDrawSettings
        {
            public float pixelSize = 64;
            public Matrix matrix = Matrix.Identity;
            public Color color = Color.White;

            public float horizontalAlign = 0F;
            public float verticalAlign = 0F;
            public bool rtl = false;
        }

        public delegate void MSDFFontDrawCall(SpriteBatch batch, Matrix? matrix);
        protected Queue<MSDFFontDrawCall> drawCalls = new Queue<MSDFFontDrawCall>();
        
        /// <summary>
        /// Attempts to guess a best fit for a bounding box around the text
        /// </summary>
        /// <param name="text"></param>
        /// <param name="pixelSize"></param>
        /// <returns></returns>
        public RectangleF MeasureString(string text)
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
                    //scale.Y += LineHeight * pixelSize;
                    
                    carriage = 0.0F;
                    line += LineHeight;
                    
                    continue;
                }
                
                if (GlyphTable.TryGetValue(c, out MSDFGlyph glyph))
                {
                    float size = MathF.Max(glyph.advance, glyph.Width);

                    // TODO: Precalculate this
                    if (glyph.planeBounds != null)
                    {
                        testRect = RectangleF.FromLTRB(glyph.planeBounds.left, glyph.planeBounds.top,
                            glyph.planeBounds.right, glyph.planeBounds.bottom);

                        testRect.X += carriage;
                        testRect.Y += line;
                        
                        rect = RectangleF.Union(testRect, rect);
                    }

                    carriage += glyph.advance;
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

        public void Draw(string text, Matrix matrix, float halign = 0F, float valign = 0F, float pixelSize = 64, Color color = default, bool rtl = false)
        {
            MSDFDrawSettings settings = new MSDFDrawSettings()
            {
                pixelSize = pixelSize,
                matrix = matrix,
                color = color,
                horizontalAlign = halign,
                verticalAlign = valign,
                rtl = rtl
            };

            Draw(text, settings);
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

        public void Draw(string text, MSDFDrawSettings settings)
        {
            drawCalls.Enqueue((batch, matrix) =>
            {
                string capture = text;
                MSDFDrawSettings drawSettings = settings;
                ScalableFont fontCapture = this;

                //string technique = drawSettings.pixelSize > 12 ? "LargeText" : "SmallText";

                // TODO: Does this severely effect performance?
                MSDFShader.Parameters["WorldViewProjection"].SetValue(drawSettings.matrix * (matrix ?? Matrix.Identity));
                MSDFShader.Parameters["ForegroundColor"].SetValue(drawSettings.color.ToVector4());
                //MSDFShader.CurrentTechnique = MSDFShader.Techniques[technique];
                MSDFShader.CurrentTechnique.Passes[0].Apply();
                
                RectangleF bounds = MeasureString(capture);
                
                Vector2 offset = new Vector2(bounds.X, bounds.Y);
                offset += new Vector2(bounds.Width * drawSettings.horizontalAlign, bounds.Height * drawSettings.verticalAlign);
                offset *= drawSettings.pixelSize;
                
                foreach (string token in capture.Split('\n'))
                {
                    string line = token.TrimEnd(' ', '\n', '\r');

                    Vector2 position = Vector2.Zero;
                    RectangleF localScale = MeasureString(line);

                    //position -= localScale * new Vector2(0, 0);
                    position -= offset;

                    StringMesh mesh = new StringMesh(line, fontCapture, position, drawSettings, WhiteSpaceWidth);
                    mesh.Draw(ActiveGame.GraphicsDevice);
                    
                    offset.Y -= LineHeight * drawSettings.pixelSize;
                }
            });
        }

        public static void RenderAll(SpriteBatch batch, Matrix? matrix = null)
        {
            Matrix drawMatrix = matrix ?? Matrix.Identity;
            drawMatrix *= Matrix.CreateScale(1.0F, -1.0F, 1.0F);
            
            if (MSDFShader == null)
                MSDFShader = ActiveGame.Content.Load<Effect>("FieldFontEffect");

            foreach (var font in LoadedFonts.Values)
                font.Render(batch, drawMatrix);
        }

        public void Render(SpriteBatch batch, Matrix? matrix = null)
        {
            MSDFShader.Parameters["TextureSize"]?.SetValue(atlasTexture.GetSize());
            MSDFShader.Parameters["PxRange"]?.SetValue(atlas.distanceRange);
            MSDFShader.Parameters["GlyphTexture"]?.SetValue(atlasTexture);
            MSDFShader.CurrentTechnique.Passes[0].Apply();

            var previousState = ActiveGame.GraphicsDevice.RasterizerState;
            ActiveGame.GraphicsDevice.RasterizerState = RasterizerState;

            FontDrawingLayer.Matrix = matrix;
            
            FontDrawingLayer.Begin(batch);
            
            while (drawCalls.Count > 0)
            {
                var call = drawCalls.Dequeue();
                call?.Invoke(batch, matrix);
            }
            
            FontDrawingLayer.End(batch);

            ActiveGame.GraphicsDevice.RasterizerState = previousState;
        }

        /// <summary>
        /// Flushes the draw queue, ensuring that vsync doesn't cause duplicates
        /// </summary>
        public void Flush()
        {
            drawCalls.Clear();
        }
    }
}
