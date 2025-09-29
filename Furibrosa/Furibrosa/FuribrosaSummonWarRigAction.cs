using RocketLib.CustomTriggers;
using UnityEngine;

namespace Furibrosa
{
    class FuribrosaSummonWarRigActionInfo : CustomTriggerActionInfo
    {
        public bool StartOnGround = true;
        public GridPoint SpawnPoint = new GridPoint( 0, 0 );
        public GridPoint TargetPoint = new GridPoint( 0, 0 );
        public override void ShowGUI( LevelEditorGUI gui )
        {
            if ( GUILayout.Button( string.Concat( new object[]
        {
            "Set Spawn Point (currently C ",
            this.SpawnPoint.collumn,
            " R ",
            this.SpawnPoint.row,
            ")"
        } ), new GUILayoutOption[0] ) )
            {
                gui.settingWaypoint = true;
                gui.waypointToSet = this.SpawnPoint;
                gui.MarkTargetPoint( this.SpawnPoint );
            }

            if ( GUILayout.Button( string.Concat( new object[]
        {
            "Set Target Point (currently C ",
            this.TargetPoint.collumn,
            " R ",
            this.TargetPoint.row,
            ")"
        } ), new GUILayoutOption[0] ) )
            {
                gui.settingWaypoint = true;
                gui.waypointToSet = this.TargetPoint;
                gui.MarkTargetPoint( this.TargetPoint );
            }

            GUILayout.Space( 5 );

            StartOnGround = GUILayout.Toggle( StartOnGround, "Spawn on the nearest ground below" );
        }
    }

    public class FuribrosaSummonWarRigAction : CustomTriggerAction
    {
        FuribrosaSummonWarRigActionInfo info;

        public override TriggerActionInfo Info
        {
            get
            {
                return this.info;
            }
            set
            {
                this.info = (FuribrosaSummonWarRigActionInfo)value;
            }
        }

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

            Vector3 spawnPoint = Map.GetBlockCenter( this.info.SpawnPoint );
            Vector3 targetPoint = Map.GetBlockCenter( this.info.TargetPoint );
            float direction = Mathf.Sign( targetPoint.x - spawnPoint.x );

            WarRig currentWarRig = UnityEngine.Object.Instantiate<WarRig>( Furibrosa.warRigPrefab, spawnPoint, Quaternion.identity );
            if ( this.info.StartOnGround )
            {
                currentWarRig.SetToGround();
            }
            currentWarRig.SetTarget( summoner, targetPoint.x, new Vector3( direction, currentWarRig.transform.localScale.y, currentWarRig.transform.localScale.z ), direction, true );
            currentWarRig.gameObject.SetActive( true );

            summoner?.currentWarRig = currentWarRig;

            this.state = TriggerActionState.Done;
        }

        public override void Update()
        {
        }
    }
}
