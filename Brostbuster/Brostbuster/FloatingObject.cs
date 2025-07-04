using UnityEngine;

namespace Brostbuster
{
    class FloatingObject
    {
        public const float moveSpeed = 35f;
        public float verticalMoveSpeed = 25f;
        public float curMoveSpeed;
        public float lerpSpeed = 10f;
        public float curLerpSpeed = 1f;
        public bool slowingDown = false;

        public GhostTrap trap;
        public BroforceObject trappedObject;
        public Vector3 grabbedPosition;
        public Vector3 currentPosition;
        public float currentRotation = 0f;
        public float rotationSpeed;
        bool movingRight;
        public float targetHeight;
        public float leftMostX, rightMostX;
        public float distanceToCenter = 0f;
        public bool reachedStartingHeight = false;
        public float height = 0f;

        
        public FloatingObject(BroforceObject grabbedObject, GhostTrap parentTrap)
        {
            trap = parentTrap;
            trappedObject = grabbedObject;
            grabbedPosition = trappedObject.transform.position;
            currentPosition = trappedObject.transform.position;
            currentRotation = 0f;
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
                targetHeight = trap.Y + 30f;
            }
            else
            {
                targetHeight = currentPosition.y + 15f;
            }

            // Determine left and right boundaries
            DetermineLimits();

            // Unit close to right edge or past right edge, move left
            if (Tools.FastAbsWithinRange(currentPosition.x - rightMostX, 30f) || (currentPosition.x - rightMostX > 0f) )
            {
                movingRight = false;
            }
            // Unit close to left edge or past left edge, move right
            else if (Tools.FastAbsWithinRange(currentPosition.x - leftMostX, 30f) || (currentPosition.x - leftMostX) < 0f)
            {
                movingRight = true;
            }
            // Move unit random direction
            else
            {
                movingRight = UnityEngine.Random.value > 0.5f;
            }

            // Store unit height
            if ( grabbedObject is Unit grabbedUnit )
            {
                this.height = grabbedUnit.height;
            }
        }

        public void DetermineLimits()
        {
            float slope = (trap.topFloatingY - trap.Y) / (trap.leftFloatingX - trap.X);
            leftMostX = (targetHeight - (trap.Y - trap.X * slope)) / slope;
            rightMostX = (trap.X - leftMostX) + trap.X;
        }

        public void MoveObject(float t)
        {
            if ( this.trappedObject == null )
            {
                RemoveObject();
                return;
            }
            // Close enough to begin swallow
            if ( reachedStartingHeight && currentPosition.y < trap.Y + 30f )
            {
                MoveUnitToCenter(t);
                return;
            }

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
            else
            {
                reachedStartingHeight = true;
                verticalMoveSpeed = 5f;
                targetHeight -= 5f;
                DetermineLimits();
            }

            num = currentPosition.x - rightMostX;
            float num2 = currentPosition.x - leftMostX;
            // Unit close to right edge, move left
            if (movingRight && Tools.FastAbsWithinRange(num, 5f))
            {
                movingRight = false;
                slowingDown = false;
            }
            // Unit close to left edge, move right
            else if (!movingRight && Tools.FastAbsWithinRange(num2, 5f))
            {
                movingRight = true;
                slowingDown = false;
            }
            
            if (movingRight && (Tools.FastAbsWithinRange(num, 10f) || slowingDown))
            {
                if ( !slowingDown )
                {
                    curMoveSpeed = moveSpeed;
                    slowingDown = true;
                    currentPosition.x += t * moveSpeed;
                    curLerpSpeed = 3f;
                }
                else
                {
                    curLerpSpeed = Mathf.Lerp(curLerpSpeed, lerpSpeed, 2f);
                    curMoveSpeed = Mathf.Lerp(curMoveSpeed, -moveSpeed, t * curLerpSpeed);

                    currentPosition.x += t * curMoveSpeed;
                    if ( Tools.FastAbsWithinRange(curMoveSpeed - (-moveSpeed), 2f) )
                    {
                        movingRight = false;
                        slowingDown = false;
                    }
                }
            }
            else if (!movingRight && (Tools.FastAbsWithinRange(num2, 10f) || slowingDown))
            {
                if (!slowingDown)
                {
                    curMoveSpeed = -moveSpeed;
                    slowingDown = true;
                    currentPosition.x += t * moveSpeed;
                    curLerpSpeed = 3f;
                }
                else
                {
                    curLerpSpeed = Mathf.Lerp(curLerpSpeed, lerpSpeed, 2f);
                    curMoveSpeed = Mathf.Lerp(curMoveSpeed, moveSpeed, t * curLerpSpeed);

                    currentPosition.x += t * curMoveSpeed;
                    if (Tools.FastAbsWithinRange(curMoveSpeed - (moveSpeed), 2f))
                    {
                        movingRight = true;
                        slowingDown = false;
                    }
                }
            }
            else if (movingRight)
            {
                currentPosition.x += t * moveSpeed;
            }
            else
            {
                currentPosition.x -= t * moveSpeed;
            }

            // Ensure unit isn't moving
            trappedObject.X = currentPosition.x;
            trappedObject.Y = currentPosition.y;
            // Move unit visually
            trappedObject.transform.position = currentPosition;
            trappedObject.transform.rotation = Quaternion.identity;

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

            trappedObject.transform.RotateAround(trappedObject.transform.position + new Vector3(0, height), Vector3.forward, currentRotation);
        }

        public void MoveUnitToCenter(float t)
        {
            if (this.trappedObject == null)
            {
                RemoveObject();
                return;
            }
            if ( distanceToCenter == 0f )
            {
                distanceToCenter = Vector3.Distance(currentPosition, trap.transform.position);
            }
            // Move unit towards center
            currentPosition = Vector3.MoveTowards(currentPosition, trap.transform.position, moveSpeed * t);

            // Ensure unit isn't moving
            trappedObject.X = currentPosition.x;
            trappedObject.Y = currentPosition.y;
            // Move unit visually
            trappedObject.transform.position = currentPosition;
            trappedObject.transform.rotation = Quaternion.identity;

            trappedObject.transform.RotateAround(trappedObject.transform.position + new Vector3(0, height), Vector3.forward, currentRotation);

            float currentDistance = Vector3.Distance(currentPosition, trap.transform.position);

            // Scale down object
            if ( currentDistance > 2f )
            {
                trappedObject.transform.localScale = new Vector3(currentDistance / distanceToCenter, currentDistance / distanceToCenter, 1f);
            }
            else
            {
                this.ConsumeObject();
            }
        }

        public void ConsumeObject()
        {
            if ( this.trappedObject is Unit trappedUnit )
            {
                ++trap.killedUnits;
                GhostTrap.grabbedUnits.Remove( trappedUnit );
            }
            else
            {
                GhostTrap.grabbedObjects.Remove( trappedObject );
            }

                trap.floatingObjects.Remove( this );
            UnityEngine.Object.Destroy( trappedObject.gameObject );
        }

        public void RemoveObject()
        {
            if ( this.trappedObject is Unit trappedUnit )
            {
                ++trap.killedUnits;
                GhostTrap.grabbedUnits.Remove( trappedUnit );
            }
            else
            {
                GhostTrap.grabbedObjects.Remove( trappedObject );
            }

            trap.floatingObjects.Remove( this );
        }

        public void ReleaseObject()
        {
            trappedObject.X = currentPosition.x;
            trappedObject.Y = currentPosition.y;
            trappedObject.transform.position = currentPosition;
            trappedObject.transform.rotation = Quaternion.identity;
            trappedObject.enabled = true;
        }
    }
}
