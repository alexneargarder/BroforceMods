using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BroMakerLib;
using BroMakerLib.CustomObjects.Projectiles;
using UnityEngine;

namespace Captain_Ameribro_Mod
{
    public class PockettedShield : CustomPockettedSpecial
    {
        public Shield shieldPrefab;
        public LayerMask groundLayer;

        public PockettedShield()
        {
            shieldPrefab = CustomProjectile.CreatePrefab<Shield>( new List<Type>() { typeof( SphereCollider ) } );
            groundLayer = ( 1 << LayerMask.NameToLayer( "Ground" ) | 1 << LayerMask.NameToLayer( "LargeObjects" ) | 1 << LayerMask.NameToLayer( "IndestructibleGround" ) );
        }

        public override void UseSpecial( BroBase bro )
        {
            Shield thrownShield;

            if ( Physics.Raycast( bro.transform.position, Vector3.up, out _, 22, this.groundLayer ) )
            {
                thrownShield = ProjectileController.SpawnProjectileLocally( this.shieldPrefab, bro, bro.X + bro.transform.localScale.x * 6f, bro.Y + 10f, bro.transform.localScale.x * 400f, 0f, false, bro.playerNum, false, false, 0f ) as Shield;
            }
            else
            {
                thrownShield = ProjectileController.SpawnProjectileLocally( this.shieldPrefab, bro, bro.X + bro.transform.localScale.x * 6f, bro.Y + 15f, bro.transform.localScale.x * 400f, 0f, false, bro.playerNum, false, false, 0f ) as Shield;
            }

            thrownShield.Setup( bro, 0f, false );
        }


        public override void SetSpecialMaterials( BroBase bro )
        {
            string directoryPath = Path.GetDirectoryName( Assembly.GetExecutingAssembly().Location );
            Material shieldIcon = ResourcesController.GetMaterial( directoryPath, "captainAmeribroSpecial.png" );
            BroMakerUtilities.SetSpecialMaterials( bro.playerNum, shieldIcon, Vector2.zero, 0 );
        }

        public override bool RefreshAmmo()
        {
            return false;
        }
    }
}
