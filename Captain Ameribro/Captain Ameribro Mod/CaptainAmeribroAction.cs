using System;
using System.Collections.Generic;
using BroMakerLib.CustomObjects.Projectiles;
using RocketLib.CustomTriggers;
using UnityEngine;

namespace Captain_Ameribro_Mod
{
    public class CaptainAmeribroActionInfo : LevelStartTriggerActionInfo
    {
        public GridPoint SpawnPoint = new GridPoint( 0, 0 );
        public bool PermanentlyGrantShield = false;
        public override void ShowGUI( LevelEditorGUI gui )
        {
            base.ShowGUI( gui );

            ShowGridPointOption( gui, this.SpawnPoint, "Set Spawn Point" );

            GUILayout.Space( 10 );

            PermanentlyGrantShield = GUILayout.Toggle( PermanentlyGrantShield, "Keep shield on respawn" );
        }
    }

    public class CaptainAmeribroAction : LevelStartTriggerAction<CaptainAmeribroActionInfo>
    {
        public static Shield shieldPrefab = CustomProjectile.CreatePrefab<Shield>( new List<Type>() { typeof( SphereCollider ) } );
        protected override void ExecuteAction( bool isLevelStart )
        {
            Vector3 spawnPoint = Map.GetBlockCenter( this.info.SpawnPoint );

            Shield shield = ProjectileController.SpawnProjectileLocally( shieldPrefab, null, spawnPoint.x, spawnPoint.y, 0f, 0f, false, 0, false, false, 0f ) as Shield;
            shield.Setup( null );
            shield.dropping = true;
            shield.stopSeeking = true;
            shield.spawnedByTrigger = true;
            shield.permanentlyGrantShield = this.info.PermanentlyGrantShield;
        }
    }
}

