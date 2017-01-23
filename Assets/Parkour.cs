using UnityEngine;
using System.Collections;
using System;

public class Parkour : MonoBehaviour {

    public float moveSpeed = 3f;
    public float momentumMultiplier = 1.2f;
    public float raycastDistance = 5f;
    public float raycastWallRunDistance = 9f;
    private Rigidbody rb;
    private CapsuleCollider capColl;
    private Renderer rendrr;

    private float colliderHeight;

    private Vector3 move;
    private Vector3 jump;
    public float jumpForce = 30f;
    private int jumpingFrames;
    private bool jumping {
        get { return jumpingFrames > 0; }
        set { jumpingFrames = 0; }
    }

    private RaycastHit hit;
    private RaycastHit sideHit;
    private RaycastHit groundCheckHit;
    private RaycastHit wallRunHit;
    private KeyCode wallRunKey = KeyCode.Space;

    public float vaultHeightMax = 10f;
    private bool vaulting;

    public Transform targetDebug;

    public float mouseRotateSensitivity = .5f;

    public float _vaultHorizontalVelocity = 18f;
    public float wallRunMinVelocitySquared = 100f;
    private bool wallRunning;
    private Quaternion wallRunRo = Quaternion.identity;

    private LineRenderer debugLR;
    public float _wallStickinessMultiplier;

    private Color defaultColor;
    private Vector3 normalizedGravity;
    private float gravityMagnitude;

    private bool wallRunTransition;
    public Transform debugJumpTarget;

    private Quaternion previousRotation;

    public void Awake() {
        rb = GetComponent<Rigidbody>();
        capColl = GetComponent<CapsuleCollider>();
        rendrr = GetComponent<Renderer>();
        defaultColor = rendrr.material.color;
        debugLR = GetComponent<LineRenderer>();
        colliderHeight = capColl.bounds.extents.y * 2f;
        normalizedGravity = Physics.gravity.normalized;
        gravityMagnitude = Physics.gravity.magnitude;
        previousRotation = rb.transform.rotation;
    }

    public void Update() {
        
        move = Vector3.zero;
        if(Input.GetKey(KeyCode.W)) {
            move += transform.forward;
        }if(Input.GetKey(KeyCode.S)) {
            move += transform.forward * -1f;
        }if(Input.GetKey(KeyCode.D)) {
            move += transform.right;
        }if(Input.GetKey(KeyCode.A)) {
            move += transform.right * -1f;
        }

        jump = Vector3.zero;
        if(jumpingFrames <= 0 && Input.GetKeyDown(KeyCode.Space)) {
            jump = rb.transform.up * jumpForce * rb.mass;
        }
	}

    private Vector3 scaledMove {
        get { return move * rb.mass * (moveSpeed + Mathf.Min(moveSpeed, rb.velocity.sqrMagnitude/10f * momentumMultiplier)); }
    }

    private Vector3 moveProjectedOnGroundPreserveMagnitude(Vector3 groundNormal, Vector3 _move) {
        Vector3 unitProjection = Vector3.Cross(groundNormal, Vector3.Cross(_move.normalized, groundNormal));
        return unitProjection * _move.magnitude;
    }

    public void FixedUpdate() {

        float mouseX = Input.mousePosition.x - Screen.width * .5f;
        Quaternion nextRo = wallRunRo * Quaternion.Slerp(rb.rotation, Quaternion.Euler(0f, mouseX * mouseRotateSensitivity, 0f), .5f);
        rb.MoveRotation(nextRo);
        if (!vaulting) {
            if (checkGrounded(out groundCheckHit, Vector3.Lerp(rb.transform.up * -1f, Vector3.up * -1f, .5f), rb.position, capColl.bounds.extents.y * 4f)) {
                if (!wallRunTransition) {
                    rb.AddForce(moveProjectedOnGroundPreserveMagnitude(groundCheckHit.normal, scaledMove));
                    //rb.AddForce(scaledMove);
                }
                if (jump.sqrMagnitude > 0f) {
                    jumpingFrames = 10;
                    rb.AddForce(jump);
                } else if(!wallRunTransition || wallRunning) {
                    stickToInclines(groundCheckHit);
                }
            }
            if(jumpingFrames > 0) { jumpingFrames--; }
            wallRun();
        }
        vault();
        debugShowStateWithColor();
    }

    private bool isColliderInFront(float howFarInFront, out RaycastHit hit) {
        Ray forwardRay = new Ray(rb.position, rb.transform.forward);
        return Physics.Raycast(forwardRay, out hit, howFarInFront);
    }

    private bool isColliderToEitherSide(float howFarToTheSide, out RaycastHit hit) {
        Vector3 diagonalToTheSide = rb.transform.right;
        Ray sideRay = new Ray(rb.position, diagonalToTheSide);
        if(Physics.Raycast(sideRay, out hit, howFarToTheSide)) {
            return true;
        }
        diagonalToTheSide = rb.transform.right * -1f;
        sideRay = new Ray(rb.position, diagonalToTheSide);
        if(Physics.Raycast(sideRay, out hit, howFarToTheSide)) {
            return true;
        }
        return false;
    }

    private bool isColliderToEitherSideAndSlightlyAbove(float howFarToTheSide, out RaycastHit hit) {
        Vector3 diagonalToTheSide = rb.transform.right + Vector3.up;
        Ray sideRay = new Ray(rb.position, diagonalToTheSide);
        if(Physics.Raycast(sideRay, out hit, howFarToTheSide)) {
            debugWithLine(hit.point);
            return true;
        }
        diagonalToTheSide = rb.transform.right * -1f + Vector3.up;
        sideRay = new Ray(rb.position, diagonalToTheSide);
        if(Physics.Raycast(sideRay, out hit, howFarToTheSide)) {
            debugWithLine(hit.point);
            return true;
        }
        return false;
    }

    private bool checkGrounded(out RaycastHit hit, Vector3 towardsFeet, Vector3 origin, float rayLength) {
        Ray towardsFeetRay = new Ray(origin, towardsFeet);
        return Physics.Raycast(towardsFeetRay, out hit, rayLength);
    }


    private bool checkGrounded(out RaycastHit hit, Vector3 towardsFeet, Vector3 origin) {
        return checkGrounded(out hit, towardsFeet, origin, colliderHeight * 2.5f);
    }

    private bool checkGrounded( out RaycastHit hit, Vector3 towardsFeet) {
        return checkGrounded(out hit, towardsFeet, rb.position);
    }

    private bool checkGrounded(out RaycastHit hit) {
        return checkGrounded(out hit, rb.transform.up * -1f);
    }

    private void vault() {
        if (!jumping && Input.GetKey(KeyCode.Space) && isColliderInFront(raycastDistance, out hit)) {
            Collider obstacle = hit.collider;

            //is this a wall?
            if(Mathf.Abs(hit.normal.y) > .05f) {
                return; //if not don't vault
            }

            //Are we running towards this wall?
            float dotWithVel = Vector3.Dot(hit.normal, rb.velocity.normalized);
            //dot closer to -1 indicates smacking into a wall fairly directly. otherwise we might be glancing at a wall or
            // moving towards the top of a ramp
            if(dotWithVel > -.3f) { 
                return;
            }

            Vector3 top = getTopPosition(obstacle);
            if (top.y - rb.position.y < vaultHeightMax) {
                StartCoroutine(doVault(hit));
            }
        }
    }

    private void stickToInclines(RaycastHit groundHit) {
        wallRunRo = Quaternion.FromToRotation(rb.transform.up, groundHit.normal);
        wallRunRo = Quaternion.Slerp(wallRunRo, previousRotation, .7f);
        previousRotation = wallRunRo;

        if(Vector3.Dot(groundHit.normal, Vector3.up) > .8f) {
            return;
        }
        rb.AddForce(getStayOnWallForce(groundHit));
        rb.AddForce(wallRunHit.normal * -1f * rb.mass * 4f);
        return;

        Vector3 perpendicularUp = Vector3.Cross(groundHit.normal, move);
        if(Vector3.Dot(perpendicularUp, Physics.gravity) > 0f) {
            perpendicularUp *= -1;
        }

        float difDot = Vector3.Dot(rb.velocity, move);
        if(difDot > 0f) {
            Vector3 dif = rb.velocity.normalized - move;
            if (Vector3.Dot(dif, perpendicularUp) < 0f) {
                float wallStickinessFactor = rb.velocity.sqrMagnitude * _wallStickinessMultiplier / 100f;
                Vector3 perpedicularNudge = wallStickinessFactor * -Physics.gravity.y * rb.mass * Vector3.Dot(Vector3.up, perpendicularUp) * perpendicularUp;
                
                rb.AddForce(perpedicularNudge);
            }
        }
        
        
    }

    private void wallRun() {
        if(vaulting || wallRunning) {
            return;
        }
        
        if(Input.GetKey(wallRunKey) && isColliderToEitherSideAndSlightlyAbove(raycastWallRunDistance, out sideHit)) {

            //is this a wall?
            if(Mathf.Abs(sideHit.normal.y) > .5f) {
                return; //if not don't wall run
            }

            if(rb.velocity.sqrMagnitude < wallRunMinVelocitySquared) {
                return;
            }

            //Are we running parallel to this wall?
            float dotWithVel = Vector3.Dot(sideHit.normal, rb.velocity.normalized);
            //Dot product close to zero indicates relatively perpendicular vectors
            if(Mathf.Abs(dotWithVel) > .4f) {
                return;
            }

            StartCoroutine(doWallRun(sideHit));

        }
    }

    private IEnumerator doWallRun(RaycastHit sideHit) {
        if (!wallRunning) {
            wallRunning = true;
            jumping = false;
            Vector3 start = rb.position;
            Vector3 end = sideHit.point + sideHit.normal * colliderHeight * .8f;
            Vector3 dif = end - start;
            Vector3 dir = dif.normalized;

            Vector3 wallForwards = getAlongWallDirection(sideHit);
            Quaternion targetRo = Quaternion.LookRotation(wallForwards, sideHit.normal);
            //transition into wall run
            float transitionFrames = 9;
            wallRunTransition = true;
            for(int i=0; i < transitionFrames; ++i) {
                Vector3 targetUp = Vector3.Slerp(rb.transform.up, sideHit.normal, (i + 1)/transitionFrames);
                rb.AddTorque(getTorqueTowardsWall(targetUp), ForceMode.Force);
                rb.MovePosition(Vector3.Lerp(start, end, (i + 1) / transitionFrames));
                yield return new WaitForFixedUpdate();
            }
            wallRunTransition = false;

            rb.MovePosition(end);
            rb.MoveRotation(targetRo);

            int minWallRunFrames = 30;
            Vector3 findWallDirection = (targetRo * Vector3.up) * -1f;
            bool wallRunKeyWasReleased = false;
            Quaternion previousRo = targetRo;

            while (checkGrounded(out wallRunHit, findWallDirection, rb.position, colliderHeight * 3f) || minWallRunFrames > 0 ) {
                minWallRunFrames--;
                rb.AddForce(getStayOnWallForce(wallRunHit));
                rb.AddForce(wallRunHit.normal * -1f * rb.mass * 4f);
                wallRunRo = Quaternion.FromToRotation(rb.transform.up, wallRunHit.normal);
                wallRunRo = Quaternion.Slerp(wallRunRo, previousRo, .8f);
                previousRo = wallRunRo;

                if(minWallRunFrames < 0) {
                    findWallDirection = wallRunHit.normal * -1f;
                }

                if(!wallRunKeyWasReleased) {
                    wallRunKeyWasReleased = !Input.GetKey(wallRunKey);
                }

                if(wallRunKeyWasReleased && Input.GetKey(wallRunKey)) {
                    //pop off wall
                    rb.AddForce(rb.transform.up * .5f * moveSpeed * rb.mass);
                    break;
                }
                
                yield return new WaitForEndOfFrame();
            }

            wallRunRo = Quaternion.identity;

            while (Input.GetKey(wallRunKey)) {
                yield return new WaitForEndOfFrame();
            }
            wallRunning = false;
        }
    }

    private Vector3 getAlongWallDirection(RaycastHit hit) {
        Vector3 wallForwards = Vector3.Cross(hit.collider.transform.up, hit.normal);
        if(Vector3.Dot(wallForwards, rb.velocity) < 0f) {
            wallForwards *= -1f;
        }
        return wallForwards;
    }

    private Vector3 getStayOnWallForce(RaycastHit wallHit) {
        float howSteep = 1f - Vector3.Dot(-1f * normalizedGravity, wallHit.normal);
        return -1f * Physics.gravity * rb.mass * (howSteep - rb.velocity.y);
    }

    private Vector3 getTorqueTowardsWall(Vector3 targetUp) {
        //credit: answers.unity3d.com/questions/48836/determining-the-torque-needed-to-rotate-an-object.html
        Vector3 x = Vector3.Cross(rb.transform.up, targetUp);
        float theta = Mathf.Asin(x.magnitude);
        Vector3 w = x.normalized * theta; // / Time.fixedDeltaTime;

        Quaternion q = rb.transform.rotation * rb.inertiaTensorRotation;
        Vector3 result = q * Vector3.Scale(rb.inertiaTensor, (Quaternion.Inverse(q) * w));
        return result;
    }

    private IEnumerator doVault(RaycastHit vaultHit) {
        if (!vaulting) {
            vaulting = true;

            Vector3 start = rb.position;
            Vector3 end = hit.point;
            Vector3 top = getTopPosition(hit.collider);

            //Aim for a point above the part of the wall that we are moving towards
            end.y = top.y + capColl.bounds.extents.y * 3.5f; // extents = half of collider's size

            Vector3 dif = end - start;
            Vector3 dir = dif.normalized;
            float horizontalDistanceToWall = (hit.point - start).magnitude;
            float hvel = _vaultHorizontalVelocity;

            //Calculate vault duraction
            //time = distance / velocity
            float vaultDuration = horizontalDistanceToWall / hvel;

            //but artificially make sure vault duration is at least one fixed frame and at most 1 second
            vaultDuration = Mathf.Clamp(vaultDuration, Time.fixedDeltaTime, 1f);

            //if we changed vault duration, change hvel proportionally. (If we didn't, we're doing some pointless math but that's OK.)
            //velocity = distance / time
            hvel = horizontalDistanceToWall / vaultDuration; 

            float vvel = dif.y / vaultDuration;
            float vel = Mathf.Sqrt(hvel * hvel + vvel * vvel);

            Vector3 velBeforeVault = rb.velocity;
            velBeforeVault.y = 0f;

            float vaultTime = vaultDuration; 
            while(vaultTime > 0f) {
                rb.velocity = dir * vel;
                vaultTime -= Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            //Set player moving again in the velocity they had pre-vault
            rb.velocity = velBeforeVault;

            //Wait a little so that vaults aren't unintentionally followed by jumps
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            vaulting = false;
        }
    }

    public void placeTargetDebug(Vector3 pos) {
        if(targetDebug) {
            targetDebug.position = pos;
        }
    }

    public static Vector3 forceTowards(Rigidbody rb, Vector3 start, Vector3 end) {
        Vector3 dif = end - start;
        Vector3 result = dif;
        float dragFudge = Mathf.Pow(rb.drag, 5f);
        result.y = Mathf.Sqrt(Mathf.Abs(dif.y) * -2f * Physics.gravity.y * rb.mass) * Mathf.Sign(dif.y);
        result.x = (-Physics.gravity.y * rb.mass / result.y) * dif.x;
        result.z = (-Physics.gravity.y * rb.mass / result.y) * dif.z;
        result *= dragFudge;
        return result;
    }
    

    private Vector3 getTopPosition(Collider obstacle) {
        return obstacle.bounds.extents;
    }

    private void debugWithLine(Vector3 end) {
        debugWithLine(rb.transform.position, end);
    }

    private void debugWithLine(Vector3 start, Vector3 end) {
        debugLR.SetPosition(0, start);
        debugLR.SetPosition(1, end);
    }

    private void debugShowStateWithColor() {
        if (wallRunTransition) {
            rendrr.material.color = Color.blue;
        } else if (wallRunning) {
            rendrr.material.color = Color.green;
        } else if (jumping) {
            rendrr.material.color = Color.cyan;
        } else if (vaulting) {
            rendrr.material.color = Color.red;
        } else {
            rendrr.material.color = defaultColor;
        }
    }
}
