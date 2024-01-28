using System;
using System.Collections.Generic;
using UnityEngine;
using BroMakerLib;
using BroMakerLib.Loggers;
using System.Reflection;
using System.IO;
using HarmonyLib;



namespace EctoBro
{
    class FloatingUnit
    {
        public const float moveSpeed = 35f;
        public const float verticalMoveSpeed = 25f;

        public GhostTrap trap;
        public Unit unit;
        public Vector3 grabbedPosition;
        public Vector3 currentPosition;
        public float currentRotation;
        public float rotationSpeed;
        bool movingRight;
        public float targetHeight;
        public float leftMostX, rightMostX;
        public float distanceToCenter = 0f;
        
        public FloatingUnit(Unit grabbedUnit, GhostTrap parentTrap)
        {
            trap = parentTrap;
            unit = grabbedUnit;
            grabbedPosition = unit.transform.position;
            currentPosition = unit.transform.position;
            currentRotation = 0;
            if (UnityEngine.Random.value > 0.5f)
            {
                rotationSpeed = UnityEngine.Random.Range(-40f, -10f);
            }
            else
            {
                rotationSpeed = UnityEngine.Random.Range(10f, 40f);
            }

            // Unit needs to move down
            if (currentPosition.y > trap.topFloatingY)
            {
                targetHeight = trap.topFloatingY - 10f;
            }
            // Unit needs to maintain current height
            else if (currentPosition.y + 20f > trap.topFloatingY)
            {
                targetHeight = currentPosition.y + 5f;
            }
            // Unit needs to move up
            else if ( currentPosition.y < trap.Y )
            {
                targetHeight = trap.Y + 10f;
            }
            else
            {
                targetHeight = currentPosition.y + 15f;
            }

            // Determine left and right boundaries
            DetermineLimits();

            // Unit close to right edge, move left
            if (Tools.FastAbsWithinRange(currentPosition.x - rightMostX, 30f))
            {
                movingRight = false;
            }
            // Unit close to left edge, move right
            else if (Tools.FastAbsWithinRange(currentPosition.x - leftMostX, 30f))
            {
                movingRight = true;
            }
            // Move unit random direction
            else
            {
                movingRight = UnityEngine.Random.value > 0.5f;
            }
        }

        public void DetermineLimits()
        {
            float slope = (trap.topFloatingY - trap.Y) / (trap.leftFloatingX - trap.X);
            leftMostX = (targetHeight - (trap.Y - trap.X * slope)) / slope;
            rightMostX = (trap.X - leftMostX) + trap.X;
        }

        public void MoveUnit(float t)
        {
            // Move unit to target height
            float num = currentPosition.y - targetHeight;
            // Move unit down
            if ( num > 0.2f )
            {
                currentPosition.y -= t * verticalMoveSpeed;
            }
            // Move unit up
            else if ( num < -0.2f )
            {
                currentPosition.y += t * verticalMoveSpeed;
            }
            
            if ( movingRight )
            {
                currentPosition.x += t * moveSpeed;
            }
            else
            {
                currentPosition.x -= t * moveSpeed;
            }

            // Unit close to right edge, move left
            if (Tools.FastAbsWithinRange(currentPosition.x - rightMostX, 5f))
            {
                movingRight = false;
            }
            // Unit close to left edge, move right
            else if (Tools.FastAbsWithinRange(currentPosition.x - leftMostX, 5f))
            {
                movingRight = true;
            }

            // Ensure unit isn't moving
            unit.X = grabbedPosition.x;
            unit.Y = grabbedPosition.y;
            // Move unit visually
            unit.transform.position = currentPosition;
            unit.transform.rotation = Quaternion.identity;

            // Rotate unit
            currentRotation += rotationSpeed * t;
            if ( currentRotation >= 360 )
            {
                currentRotation -= 360f;
            }
            else if ( currentRotation <= -360f )
            {
                currentRotation += 360f;
            }

            unit.transform.RotateAround(unit.transform.position + new Vector3(0, unit.height), Vector3.forward, currentRotation);
        }

        public void MoveUnitToCenter(float t)
        {
            if ( distanceToCenter == 0f )
            {
                distanceToCenter = Vector3.Distance(currentPosition, trap.transform.position);
            }
            // Move unit towards center
            currentPosition = Vector3.MoveTowards(currentPosition, trap.transform.position, moveSpeed * t);

            // Ensure unit isn't moving
            unit.X = grabbedPosition.x;
            unit.Y = grabbedPosition.y;
            // Move unit visually
            unit.transform.position = currentPosition;
            unit.transform.rotation = Quaternion.identity;

            unit.transform.RotateAround(unit.transform.position + new Vector3(0, unit.height), Vector3.forward, currentRotation);

            float currentDistance = Vector3.Distance(currentPosition, trap.transform.position);

            // Scale down object
            if ( currentDistance > 2f )
            {
                unit.transform.localScale = new Vector3(currentDistance / distanceToCenter, currentDistance / distanceToCenter, 1f);
            }
            else
            {
                //unit.transform.localScale = new Vector3(currentDistance / distanceToCenter, currentDistance / distanceToCenter, 1f);
                this.ConsumeUnit();
            }
            

/*            // Rotate unit
            currentRotation += rotationSpeed * t;
            if (currentRotation >= 360)
            {
                currentRotation -= 360f;
            }
            else if (currentRotation <= -360f)
            {
                currentRotation += 360f;
            }

            unit.transform.RotateAround(unit.transform.position + new Vector3(0, unit.height), Vector3.forward, currentRotation);*/
        }

        public void ConsumeUnit()
        {
            trap.floatingUnits.Remove(this);
            GhostTrap.grabbedUnits.Remove(this.unit);
            UnityEngine.Object.Destroy(unit.gameObject);
        }

        public void ReleaseUnit()
        {
            unit.X = currentPosition.x;
            unit.Y = currentPosition.y;
            unit.transform.position = currentPosition;
            unit.transform.rotation = Quaternion.identity;
        }
    }
}
