using BroMakerLib.CustomObjects.Projectiles;
using System.IO;
using UnityEngine;

namespace Drunken_Broster.MeleeItems
{
    public class BottleProjectile : CustomGrenade
    {
        protected BloodColor bloodColor = BloodColor.Green;
        protected float explodeRange = 40f;
        protected AudioClip[] deathSounds;

        protected override void Awake()
        {
            this.size = 8f;
            this.damage = 5;

            base.Awake();

            string soundPath = Path.Combine( spriteAssemblyPath, "sounds" );

            SoundHolder doubleBroSevenSounds = ( HeroController.GetHeroPrefab( HeroType.DoubleBroSeven ) as DoubleBroSeven ).martiniGlass.soundHolder;

            // Load death sound
            this.deathSounds = doubleBroSevenSounds.deathSounds;
        }

        protected override void Bounce( bool bounceX, bool bounceY )
        {
            this.Death();
        }

        public override void Death()
        {
            this.MakeEffects();
            this.DestroyGrenade();
        }

        protected override void MakeEffects()
        {
            EffectsController.CreateGlassShards( base.X, base.Y, 20, 4f, 4f, 80f, 60f, this.xI * 0.2f, 50f, 0.2f, 1f, 0.3f, 0.5f );
        }

    }
}
