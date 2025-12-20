using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Planet9.Entities
{
    public class Particle
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public Color Color { get; set; }
        public float Life { get; set; } // 0.0 to 1.0, 1.0 = full life, 0.0 = dead
        public float Size { get; set; }
        public float LifeTime { get; set; } // Total lifetime in seconds
        public float Age { get; set; } // Current age in seconds

        public bool IsAlive => Life > 0f;

        public void Update(float deltaTime)
        {
            Age += deltaTime;
            Life = 1f - (Age / LifeTime);
            
            if (Life < 0f)
                Life = 0f;

            // Update position
            Position += Velocity * deltaTime;
            
            // Fade out velocity (drag)
            Velocity *= 0.95f;
        }
    }
}

