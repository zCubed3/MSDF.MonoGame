using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MSDF.MonoGame
{
    public class DrawString
    {
        public const float DegreeToRadians = 0.01745329251f;
        
        private StringMesh _stringMesh = new();

        private float _angle = 0.0F;
        
        private string _fontName = null;
        private TextDrawRecipe _recipe = new()
        {
            CharacterSpacing = 1.0F,
            LineSpacing = 1.0F,
            
            HorizontalAlign = 0.5F,
            VerticalAlign = 0.5F,
            Color = Color.White.ToVector4(),
            PixelSize = 64,
            
            Text = "",
            
            ModelMatrix = Matrix.Identity,
            RightToLeft = false
        };
        
        public string Text
        {
            get => _recipe.Text;
            set
            {
                _recipe.Text = value;
                _stringMesh.Rebuild(_recipe, ScalableFont.LoadedFonts[_fontName]);
            }
        }
        
        public float HorizontalAlign
        {
            get => _recipe.HorizontalAlign;
            set
            {
                _recipe.HorizontalAlign = value;
                _stringMesh.Rebuild(_recipe, ScalableFont.LoadedFonts[_fontName]);
            }
        }
        
        public float VerticalAlign
        {
            get => _recipe.VerticalAlign;
            set
            {
                _recipe.VerticalAlign = value;
                _stringMesh.Rebuild(_recipe, ScalableFont.LoadedFonts[_fontName]);
            }
        }

        public Vector3 Position
        {
            get => _recipe.ModelMatrix.Translation;
            set => _recipe.ModelMatrix.Translation = value;
        }

        public float Angle
        {
            get => _angle;
            set
            {
                _angle = value;

                float rad = _angle * DegreeToRadians;

                _recipe.ModelMatrix.Decompose(out Vector3 scale, out Quaternion _, out Vector3 translation);
                _recipe.ModelMatrix = Matrix.CreateFromYawPitchRoll(0.0F, 0.0F, rad);
                _recipe.ModelMatrix.Translation = translation;
                _recipe.ModelMatrix *= Matrix.CreateScale(scale);
            }
        }
        
        public DrawString(ScalableFont font, string text)
        {
            _fontName = font.FontName;
            this.Text = text;
        }

        public void EnqueueDraw()
        {
            ScalableFont.LoadedFonts[_fontName].Draw(this);
        }

        /// <summary>
        /// Actually draws the scalable font
        /// </summary>
        public void Draw(ref GraphicsDevice graphicsDevice)
        {
            if (_stringMesh == null)
                return;
            
            string technique = _recipe.PixelSize > 32 ? "LargeText" : "SmallText";
            
            ScalableFont.MSDFShader.Parameters["WorldMatrix"].SetValue(_recipe.ModelMatrix);
            ScalableFont.MSDFShader.Parameters["ForegroundColor"].SetValue(_recipe.Color);
            ScalableFont.MSDFShader.CurrentTechnique = ScalableFont.MSDFShader.Techniques[technique];
            ScalableFont.MSDFShader.CurrentTechnique.Passes[0].Apply();
            
            _stringMesh.Draw(ref graphicsDevice);
        }
    }
}