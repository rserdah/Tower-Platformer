using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modified from Brackey's tutorial on YouTube ("THIRD PERSON MOVEMENT in Unity": https://www.youtube.com/watch?v=4HpC--2iowE)
/// </summary>
public class TPSPlayer : MonoBehaviour
{
    [Serializable]
    public class Controls
    {
        public TPSPlayer player;

        //Tuples won't be in Inspector
        public (string strafe, string frontBack) movementAxes = ("Horizontal", "Vertical");
        public (float strafe, float frontBack) movementInput;

        public (string x, string y) lookAxes = ("Mouse X", "Mouse Y");
        public (float x, float y) lookInput;

        //Can make KeyCode tuple with main KeyCode and alt KeyCode
        public (KeyCode main, KeyCode alt) jump = (KeyCode.Space, KeyCode.None);
        public (KeyCode main, KeyCode alt) run = (KeyCode.LeftShift, KeyCode.RightShift);


        public void Initialize(TPSPlayer player)
        {
            this.player = player;
        }

        public void GetInput()
        {
            movementInput.strafe = Input.GetAxisRaw(movementAxes.strafe); //Maybe also do * Time.deltaTime
            movementInput.frontBack = Input.GetAxisRaw(movementAxes.frontBack); //Maybe also do * Time.deltaTime

            //Old
            //lookInput.x = Input.GetAxis(lookAxes.x) * player.settings.mouseSpeed * Time.deltaTime;
            //lookInput.y = Input.GetAxis(lookAxes.y) * player.settings.mouseSpeed * Time.deltaTime;

            //New
            lookInput.x = lookInput.y = 0;
        }

        public bool Jump()
        {
            return GetKeyDown(jump);
        }

        public bool Run()
        {
            return GetKey(run);
        }

        public static bool GetKey((KeyCode main, KeyCode alt) keyCodeTuple)
        {
            return Input.GetKey(keyCodeTuple.main) || Input.GetKey(keyCodeTuple.alt);
        }

        public static bool GetKeyDown((KeyCode main, KeyCode alt) keyCodeTuple)
        {
            return Input.GetKeyDown(keyCodeTuple.main) || Input.GetKeyDown(keyCodeTuple.alt);
        }

        public static bool GetKeyUp((KeyCode main, KeyCode alt) keyCodeTuple)
        {
            return Input.GetKeyUp(keyCodeTuple.main) || Input.GetKeyUp(keyCodeTuple.alt);
        }
    }

    [Serializable]
    public class Settings
    {
        public float walkSpeed = 20f;
        public float runSpeed = 35f;

        public float mouseSpeed = 1f;

        public float gravity = -9.81f;
        public float jumpHeight = 2f;
        public bool airControl = true;
        public bool disableAirControlUntilLand;


        public float turnSmoothTime = 0.1f;
    }

    public class Components
    {
        //public Rigidbody rb;
        public CharacterController characterController;
        public Collider collider;


        public void GetComponents(TPSPlayer player)
        {
            //rb = player.GetComponent<Rigidbody>();
            characterController = player.GetComponent<CharacterController>();
            collider = player.GetComponent<Collider>();
        }
    }

    [Serializable]
    public class Stats
    {
        public float maxHealth = 100f;
        public float health = 100f;
        public float damage = 105f;
        public float enemyKillJumpBoost = 7.5f;
        public float getHitJumpBoost = 4.5f;
        public int maxJumps = 2;
        public int jumps = 2;
        /// <summary>
        /// The minimum amount of time before Player can get damaged by the same Enemy
        /// </summary>
        [Tooltip("The minimum amount of time before Player can get damaged by the same Enemy.")]
        public float getHitRestTime = 0.5f;

        //Bools
        public bool isGrounded;
        public bool isInSafeHouse;
        public bool isDead;

        //Extents for checking collisions (half extents, not full extents)
        public Vector3 groundCheckHalfExtents = new Vector3(0.5f, 0.5f, 0.5f); //For checking if grounded
        public Vector3 attackHalfExtents = new Vector3(0.18f, 0.18f, 0.18f); //For checking if hitting Enemy
        public Vector3 bodyHalfExtents = new Vector3(0.75f, 1.75f, 0.75f); //For checking if getting hit by an Enemy
        //TODO: Make a bounding box(called proximityHalfExtents; make it not meet the sides, but have it be tall on the y axis) to check if the player is within a certain SubLevel(so that the next Level can start once the Player is in side that bounding box instead of once they hit the SubLevel's ground)
    }

    public Controls controls = new Controls();
    public Settings settings = new Settings();
    public Components components = new Components();
    public Stats stats = new Stats();

    public new Camera camera;
    public Transform groundCheckOrigin;
    public LayerMask groundLayerMask;

    public LayerMask jumpBoostLayer;

    public Collider[] fallApartOnDeath;


    //Temp./Holder Variables
    float xRot;
    Vector3 velocityFromGravity = Vector3.zero;
    float turnSmoothVelocity;
    Quaternion quaternionZero = new Quaternion();
    Enemy lastAttacker;
    float nextTimeHit;


    private void Start()
    {
        gameObject.tag = "Player";
        gameObject.layer = LayerMask.NameToLayer("Player");

        controls.Initialize(this);
        components.GetComponents(this);

        //groundCheckHalfExtents = Vector3.one * components.characterController.radius * 0.3f;
        //attackHalfExtents = Vector3.one * components.characterController.radius * 0.3f;

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        controls.GetInput();

        Move(controls.movementInput.strafe, controls.movementInput.frontBack);
        //Look(controls.lookInput.x, controls.lookInput.y); //Don't need in TPSPlayer b/c TPS camera is controlled by Cinemachine
    }

    //private void OnControllerColliderHit(ControllerColliderHit hit)
    //{
    //    Debug.LogError($"{hit.collider.name} Hit {name}");
    //}

    private void Move(float localXInput, float localZInput)
    {
        //isGrounded = Physics.CheckSphere(groundCheckOrigin.position, components.characterController.radius, groundLayerMask);
        stats.isGrounded = GroundCheck();

        if(stats.isGrounded) //With the current ground check range, jumps are actually reset the very next frame after a jump, so if Stats.maxJumps == 2, Player can actually jump 3 times before landing
            stats.jumps = stats.maxJumps;

        if(stats.isGrounded && velocityFromGravity.y < 0f)
        {
            velocityFromGravity.x = velocityFromGravity.z = 0f;
            velocityFromGravity.y = -2f; //Set it to -2f instead of 0 b/c isGrounded can become true slightly before player actually hits the ground (depending on sphere check radius)
        }

        //Jumping
        if(stats.jumps > 0)
        {
            if(/*stats.isGrounded && */controls.Jump())
            {
                if(stats.jumps > 0)
                {
                    Jump(settings.jumpHeight);


                    stats.jumps--;
                }
            }
        }

        //Using JumpBoosts
        Collider col;
        if(col = GetGroundCheck(jumpBoostLayer))
        {
            try
            {
                JumpBoost j = col.GetComponent<JumpBoost>();
                Jump(j.GetJumpBoost(), j.GetJumpBoostDirection());
            }
            catch(Exception) { }
        }

        //Hitting Enemies (Ignore trigger b/c don't want to be able to hit Enemies by hitting their weapons (e.g. FlyingEye's weapon is laser & is trigger))
        if(col = GetCollisionCheck(groundCheckOrigin.position, stats.attackHalfExtents, "Enemy", QueryTriggerInteraction.Ignore))
        {
            Debug.LogError("Hit Enemy");

            //try { Damage(col.GetComponent<Enemy>(), stats.damage); }
            //catch(Exception) { }

            try { Damage(GetEnemyFromColliderOrParent(col), stats.damage); }
            catch(Exception) { }

            Jump(stats.enemyKillJumpBoost);
        }
        //Getting hit by Enemies
        else if((col = GetCollisionCheck(transform.position, stats.bodyHalfExtents, "Enemy")) || (col = GetCollisionCheck(transform.position, stats.bodyHalfExtents, "Hazard", QueryTriggerInteraction.Collide)))
        {
            Enemy attackingEnemy = null;


            //attackingEnemy = col.GetComponent<Enemy>();

            //if(!attackingEnemy)
            //    attackingEnemy = col.GetComponentInParent<Enemy>();

            attackingEnemy = GetEnemyFromColliderOrParent(col);

            if(attackingEnemy)
            {
                Debug.LogError("Enemy hit Player", attackingEnemy.gameObject);
                //TODO: Instead of jumping in the direction of (attackingEnemy.transform.forward + Vector3.up), jump in direction of ((transform.position - attackingEnemy.transform.position) + vector3.up) (dir. from enemy to player + up)
                if(TakeDamage(attackingEnemy))
                    Jump(stats.getHitJumpBoost, attackingEnemy.transform.forward + Vector3.up);
            }
        }
        ////Getting hit by Hazards
        //else if(col = GetCollisionCheck(transform.position, stats.bodyHalfExtents, "Hazard", QueryTriggerInteraction.Collide))
        //{
        //    TakeDamage(null, 50f);
        //    Jump(stats.getHitJumpBoost, col.transform.forward + Vector3.up);
        //}

        if(stats.isGrounded || (!stats.isGrounded && settings.airControl))
        {
            Vector3 dir = new Vector3(controls.movementInput.strafe, 0f, controls.movementInput.frontBack).normalized;

            if(dir.sqrMagnitude >= 0.1f * 0.1f)
            {
                float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + camera.transform.eulerAngles.y;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, settings.turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward; //Multiply by this Vector to turn the rotation into a direction
                components.characterController.Move(GetSpeed() * moveDir.normalized * Time.deltaTime);
            }
        }

        velocityFromGravity.y += settings.gravity * Time.deltaTime;
        components.characterController.Move(velocityFromGravity * Time.deltaTime); //Multiply velocityFromGravity by Time.deltaTime again b/c deltaY = 1/2 * g * t^2
    }

    /*private void Look(float horizontalInput, float verticalInput)
    {
        xRot -= verticalInput;
        xRot = Mathf.Clamp(xRot, -90f, 90f);

        transform.Rotate(Vector3.up * horizontalInput); //Rotate player around global Y
        camera.transform.localRotation = Quaternion.Euler(xRot, 0f, 0f); //Rotate camera around local X
    }*/

    /*//Check all GroundCheck methods
    private bool GroundCheck(Vector3 position, float radius, LayerMask layerMask)
    {
        //return Physics.CheckSphere(position, radius, layerMask);
        return Physics.CheckBox(position, Vector3.one * radius, quaternionZero, layerMask); //Physics.CheckBox may be more efficient than Physics.CheckSphere
    }

    private bool GroundCheck(Vector3 position, float radius, string tag)
    {
        //return Physics.OverlapSphere(position, radius, LayerMaskHelper.EverythingMask)[0].tag.Equals(tag);
        return Physics.OverlapBox(position, Vector3.one * radius, quaternionZero, LayerMaskHelper.EverythingMask)[0].tag.Equals(tag); //Physics.OverlapBox may be more efficient than Physics.OverlapSphere
    }

    private bool GroundCheck(LayerMask layerMask)
    {
        //Physics.OverlapBox() (which is called in this overloaded GroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GroundCheck(groundCheckOrigin.position, components.characterController.radius * 0.3f, layerMask);
    }

    private bool GroundCheck(string tag)
    {
        //Physics.OverlapBox() (which is called in this overloaded GroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GroundCheck(groundCheckOrigin.position, components.characterController.radius * 0.3f, tag);
    }

    private bool GroundCheck()
    {
        return GroundCheck(groundLayerMask);
    }

    private Collider GetGroundCheck(Vector3 position, float radius, LayerMask layerMask)
    {
        Collider col = null;

        try { col = Physics.OverlapBox(position, Vector3.one * radius, quaternionZero, layerMask)[0]; }
        catch(Exception) { }


        return col;
    }

    private Collider GetGroundCheck(Vector3 position, float radius, string tag)
    {
        Collider col = null;

        try { col = Physics.OverlapBox(position, Vector3.one * radius, quaternionZero, LayerMaskHelper.EverythingMask)[0]; }
        catch(Exception) { }


        if(col && col.tag.Equals(tag))
            return col;
        else
            return null;
    }

    private Collider GetGroundCheck(LayerMask layerMask)
    {
        //Physics.OverlapBox() (which is called in this overloaded GetGroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GetGroundCheck(groundCheckOrigin.position, components.characterController.radius * 0.3f, layerMask);
    }

    private Collider GetGroundCheck(string tag)
    {
        //Physics.OverlapBox() (which is called in this overloaded GetGroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GetGroundCheck(groundCheckOrigin.position, components.characterController.radius * 0.3f, tag);
    }*/

    //Check all GroundCheck methods
    private bool GroundCheck(Vector3 position, Vector3 halfExtents, LayerMask layerMask)
    {
        //return Physics.CheckSphere(position, radius, layerMask);
        return Physics.CheckBox(position, halfExtents, quaternionZero, layerMask); //Physics.CheckBox may be more efficient than Physics.CheckSphere
    }

    private bool GroundCheck(Vector3 position, Vector3 halfExtents, string tag)
    {
        //return Physics.OverlapSphere(position, radius, LayerMaskHelper.EverythingMask)[0].tag.Equals(tag);
        return Physics.OverlapBox(position, halfExtents, quaternionZero, LayerMaskHelper.EverythingMask)[0].tag.Equals(tag); //Physics.OverlapBox may be more efficient than Physics.OverlapSphere
    }

    private bool GroundCheck(LayerMask layerMask)
    {
        //Physics.OverlapBox() (which is called in this overloaded GroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GroundCheck(groundCheckOrigin.position, stats.groundCheckHalfExtents, layerMask);
    }

    private bool GroundCheck(string tag)
    {
        //Physics.OverlapBox() (which is called in this overloaded GroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GroundCheck(groundCheckOrigin.position, stats.groundCheckHalfExtents, tag);
    }

    private bool GroundCheck()
    {
        return GroundCheck(groundLayerMask);
    }

    private Collider GetGroundCheck(Vector3 position, Vector3 halfExtents, LayerMask layerMask)
    {
        Collider col = null;

        try { col = Physics.OverlapBox(position, halfExtents, quaternionZero, layerMask)[0]; }
        catch(Exception) { }


        return col;
    }

    private Collider GetGroundCheck(LayerMask layerMask)
    {
        //Physics.OverlapBox() (which is called in this overloaded GetGroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        return GetGroundCheck(groundCheckOrigin.position, stats.groundCheckHalfExtents, layerMask);
    }

    private Collider GetGroundCheck(string tag)
    {
        //Physics.OverlapBox() (which is called in this overloaded GetGroundCheck()) takes half extents, so have to pass in less than 1/2 (hence the * 0.3f) of the CharacterController radius or else it will collide with things on the sides of 
        //the character instead of just under the character
        //      return GetGroundCheck(groundCheckOrigin.position, groundCheckHalfExtents, tag);
        return GetCollisionCheck(groundCheckOrigin.position, stats.groundCheckHalfExtents, tag);
    }

    private Collider GetCollisionCheck(Vector3 position, Vector3 halfExtents, string tag, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
    {
        Collider col = null;

        try { col = Physics.OverlapBox(position, halfExtents, quaternionZero, LayerMaskHelper.EverythingMask, queryTriggerInteraction)[0]; }
        catch(Exception) { }


        if(col && col.tag.Equals(tag))
            return col;
        else
            return null;
    }

    private Enemy GetEnemyFromColliderOrParent(Collider col)
    {
        Enemy e = col.GetComponent<Enemy>();

        if(!e)
            e = col.GetComponentInParent<Enemy>();

        return e;
    }

    private float GetSpeed()
    {
        if(controls.Run())
            return settings.runSpeed;
        else
            return settings.walkSpeed;
    }

    //TODO: Make gravity more realistic by instead of having gravity be a certain Vector3 until landing, have it be additive (currently, if there is a Jump force in the backwards direction and the Player presses forward, they can never overcome 
    //that force until they land (i.e. once they stop pressing forward, the backward Jump force will continue to push them back in air, making it unrealistic and annoying)) !!!!!
    public void Jump(float jumpHeight)
    {
        velocityFromGravity.y = GetJumpForce(jumpHeight); //Set y component INSTEAD of adding to it
    }

    public void Jump(float jumpHeight, Vector3 direction)
    {
        velocityFromGravity = GetJumpForce(jumpHeight) * direction; //Set velocityFromGravity INSTEAD of adding to it
    }

    private float GetJumpForce(float desiredJumpHeight)
    {
        //v = sqrt(height * -2 * gravity)

        return Mathf.Sqrt(desiredJumpHeight * -2f * settings.gravity);
    }

    /// <summary>
    /// If both Enemy and float parameters are given, Enemy.stats.damage will override the float parameter
    /// </summary>
    /// <param name="e"></param>
    /// <param name="damage"></param>
    public bool TakeDamage(Enemy e = null, float damage = 0f)
    {
        if(e)
        {
            if(!e.Equals(lastAttacker))
            {
                lastAttacker = e;
                nextTimeHit = Time.time + stats.getHitRestTime;

                damage = e.stats.damage;
            }
            else if(Time.time - nextTimeHit > 0f) //If enough time passed in order to get hit by same Enemy (which is e)
            {
                damage = e.stats.damage;
            }
            else
                return false;
        }
        else
        {
            lastAttacker = null;
            nextTimeHit = 0;
        }

        if(stats.health > 0)
        {
            stats.health -= damage;
        }

        if(stats.health <= 0)
        {
            Die();
        }


        return true;
    }

    public void Damage(Enemy e, float damage)
    {
        e.TakeDamage(damage);
    }

    private void Die()
    {
        stats.isDead = true;

        foreach(Collider col in fallApartOnDeath)
        {
            col.enabled = true;
            col.gameObject.AddComponent<Rigidbody>();//.AddExplosionForce(5f, transform.position, 5f);
        }

        enabled = false;
    }

    private void OnDrawGizmosSelected()
    {
        if(components.characterController)
        {
            Gizmos.color = Color.green;

            //Gizmos.DrawWireCube(groundCheckOrigin.position, Vector3.one * components.characterController.radius);
            Gizmos.DrawWireCube(groundCheckOrigin.position, stats.attackHalfExtents * 2f);
        }

        Gizmos.color = Color.red;

        Gizmos.DrawWireCube(transform.position, stats.bodyHalfExtents * 2f);

        Gizmos.color = Color.blue;

        Gizmos.DrawLine(transform.position, transform.position + velocityFromGravity);
        Gizmos.DrawWireCube(groundCheckOrigin.position, stats.groundCheckHalfExtents * 2f);
    }
}
