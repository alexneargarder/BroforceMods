using RocketLib.CustomTriggers;
using UnityEngine;

namespace Furibrosa
{
    public class FuribrosaSummonWarRigActionInfo : CustomTriggerActionInfo
    {
        public bool StartOnGround = true;
        public GridPoint SpawnPoint = new GridPoint( 0, 0 );
        public GridPoint TargetPoint = new GridPoint( 0, 0 );

        public override void ShowGUI( LevelEditorGUI gui )
        {
            ShowGridPointOption( gui, SpawnPoint, "Set Spawn Point" );
            ShowGridPointOption( gui, TargetPoint, "Set Target Point" );

            GUILayout.Space( 5 );

            StartOnGround = GUILayout.Toggle( StartOnGround, "Spawn on the nearest ground below" );
        }
    }

    public class FuribrosaSummonWarRigAction : CustomTriggerAction<FuribrosaSummonWarRigActionInfo>
    {
        public FuribrosaSummonWarRigAction()
        {
            Furibrosa.CreateWarRigPrefab();
        }

        public override void Start()
        {
            base.Start();

            Furibrosa.CreateWarRigPrefab();

            Furibrosa summoner = null;

            for ( int i = 0; i < 4; ++i )
            {
                if ( HeroController.PlayerIsAlive( i ) )
                {
                    try
                    {
                        if ( HeroController.players[i].character is Furibrosa furibrosa )
                        {
                            summoner = furibrosa;
                            break;
                        }
                    }
                    catch { }
                }
            }

            GridPoint realPoint = new GridPoint( info.SpawnPoint.collumn - Map.lastXLoadOffset, info.SpawnPoint.row - Map.lastYLoadOffset );
            Vector3 spawnPoint = Map.GetBlockCenter( realPoint );
            realPoint = new GridPoint( info.TargetPoint.collumn - Map.lastXLoadOffset, info.TargetPoint.row - Map.lastYLoadOffset );
            Vector3 targetPoint = Map.GetBlockCenter( realPoint );
            float direction = Mathf.Sign( targetPoint.x - spawnPoint.x );

            WarRig currentWarRig = Object.Instantiate<WarRig>( Furibrosa.warRigPrefab, spawnPoint, Quaternion.identity );
            if ( info.StartOnGround )
            {
                currentWarRig.SetToGround();
            }

            currentWarRig.SetTarget( summoner, targetPoint.x, new Vector3( direction, currentWarRig.transform.localScale.y, currentWarRig.transform.localScale.z ), direction, true );
            currentWarRig.gameObject.SetActive( true );

            if ( summoner != null )
                summoner.currentWarRig = currentWarRig;

            state = TriggerActionState.Done;
        }

        public override void Update()
        {
        }
    }
}