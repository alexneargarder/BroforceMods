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
        public Vector3 currentRotation;
        public float rotationSpeed;
        bool movingRight;
        public float targetHeight;
        public float leftMostX, rightMostX;
        
        public FloatingUnit(Unit grabbedUnit, GhostTrap parentTrap)
        {
            trap = parentTrap;
            unit = grabbedUnit;
            grabbedPosition = unit.transform.position;
            currentPosition = unit.transform.position;
            currentRotation = unit.transform.eulerAngles;
            if (UnityEngine.Random.value > 0.5f)
            {
                rotationSpeed = UnityEngine.Random.Range(-20f, -8f);
            }
            else
            {
                rotationSpeed = UnityEngine.Random.Range(8f, 20f);
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
            else
            {
                targetHeight = currentPosition.y + 15f;
            }

            // Determine left and right boundaries
            DetermineLimits();

            // Unit close to right edge, move left
            if (Tools.FastAbsWithinRange(currentPosition.x - rightMostX, 20f))
            {
                movingRight = false;
            }
            // Unit close to left edge, move right
            else if (Tools.FastAbsWithinRange(currentPosition.x - leftMostX, 20f))
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

            currentRotation.z += rotationSpeed * t;

            unit.X = grabbedPosition.x;
            unit.Y = grabbedPosition.y;
            unit.transform.position = currentPosition;
            unit.transform.eulerAngles = currentRotation;
        }


    }
}
