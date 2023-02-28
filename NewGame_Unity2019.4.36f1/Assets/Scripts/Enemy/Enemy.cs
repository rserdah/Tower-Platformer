using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Enemy : MonoBehaviour
{
    public enum Type
    {
        ENEMY, ROLLER, FLYINGEYE
    }

    public class Components
    {
        //public Rigidbody rb;
        public Animator anim;
        public NavMeshAgent agent;
        public Collider collider;


        public void GetComponents(Enemy enemy)
        {
            //rb = enemy.GetComponent<Rigidbody>();
            anim = enemy.GetComponent<Animator>();
            agent = enemy.GetComponent<NavMeshAgent>();

            if(enemy.Is(Enemy.Type.FLYINGEYE))
                collider = enemy.transform.GetChild(0).GetComponent<Collider>();
            else
                collider = enemy.GetComponent<Collider>();
        }
    }

    [Serializable]
    public class Settings
    {
        public bool idlePathFollow;
        public float pointReachedBuffer = 0.15f;
        public Transform[] pathPoints;
        public float[] restTimes;

        public float detectionRadius = 3f;
        public float attackRestTime = 5f;
        public float attackDuration = 2f;
    }

    [Serializable]
    public class Stats
    {
        public float maxHealth = 100f;
        public float health = 100f;
        public float damage = 35f;

        public float attackSpeed = 5f; //Temp.

        //Bools
        public bool isAttacking;
        public bool isResting;
        public bool isDead;
    }

    public Type type;

    public Settings settings = new Settings();
    public Components components = new Components();
    public Stats stats = new Stats();

    public TPSPlayer player;
    public Level level;
    public Level.SubLevel subLevel;

    private Transform laser;

    //Temp./Holder Variables
    float sqrDist;
    int currentPoint;
    IEnumerator pathRestCoroutine;
    bool reachedPoint;
    RaycastHit laserHit;
    public LayerMask playerLayer;
    public Collider[] cols;
    float nextTimeAttack;
    (Vector3 current, Vector3 from, Vector3 to) attackPoints;
    Vector3 attackDirection;
    public Bounds frontBumper;
    public Mesh cube;
    int attackRestCount;


    private void Start()
    {
        gameObject.tag = "Enemy";
        gameObject.layer = LayerMask.NameToLayer("Enemy");

        components.GetComponents(this);

        if(Is(Type.FLYINGEYE))
        {
            laser = transform.GetChild(0).GetChild(transform.GetChild(0).childCount - 1); //Laser is last child of the first child of FlyingEye Enemy
        }
    }

    private void FixedUpdate()
    {
        //Uncomment !!!!!!!!!!!!!!!!!!

        //if(!stats.isDead && player)
        //{
        //    Vector3 heading = player.transform.position - transform.position;
        //    heading.y = 0;
        //    transform.forward = heading;

        //    if(level && !subLevel.Equals(Level.SubLevel.Empty)) //Check
        //        components.agent.destination = player.transform.position;
        //}

        //TODO: Make list of rest times to allow agent to pause after reaching certain target, Make option to make it an idle path (meaning the agent follows that path until the player is in a certain range OR it sees the player at which point it would follow the player)

        if(!stats.isDead)
        {
            if(settings.idlePathFollow)
            {
                FollowPath();
            }

            if(Is(Type.FLYINGEYE))
            {
                if((DetectsPlayer() || stats.isAttacking) && !player.stats.isInSafeHouse)
                {
                    if(stats.isAttacking)
                    {
                        attackPoints.current = Vector3.Lerp(attackPoints.current, attackPoints.to, stats.attackSpeed * Time.deltaTime);

                        if((attackPoints.current - attackPoints.to).sqrMagnitude <= 0.75f * 0.75f)
                        {
                            stats.isAttacking = false;
                            laser.gameObject.SetActive(false);
                        }

                        LookAt(attackPoints.current);

                        //Fix Raycast part below b/c it seems to collide with the laser making the laser disappear while Enemy is attacking (maybe ignore triggers b/c laser is trigger ALSO debug what Collider is being hit)
                        //Fix Raycast part below b/c it seems to collide with the laser making the laser disappear while Enemy is attacking (maybe ignore triggers b/c laser is trigger ALSO debug what Collider is being hit)
                        //Fix Raycast part below b/c it seems to collide with the laser making the laser disappear while Enemy is attacking (maybe ignore triggers b/c laser is trigger ALSO debug what Collider is being hit)
                        //Fix Raycast part below b/c it seems to collide with the laser making the laser disappear while Enemy is attacking (maybe ignore triggers b/c laser is trigger ALSO debug what Collider is being hit)
                        //Fix Raycast part below b/c it seems to collide with the laser making the laser disappear while Enemy is attacking (maybe ignore triggers b/c laser is trigger ALSO debug what Collider is being hit)

                        //ALSO FIX SO IT STARTS LERPING LASER WHEN IT FIRST SEES PLAYER BUT CONTINUES LERPING EVEN IF IT STOPS DETECTING PLAYER (NEED NEW BOOL OR USE ISATTACKING)

                        if(Physics.Raycast(laser.position, laser.forward, out laserHit, 500f, LayerMaskHelper.EverythingMask, QueryTriggerInteraction.Ignore)) //Ignore triggers b/c laser Collider is trigger and don't want to collide with self
                        {
                            Transform laser1 = laser.GetChild(0);

                            float yScale = laserHit.distance / 2f;
                            Vector3 scale = laser1.localScale;
                            scale.y = yScale;

                            laser1.localScale = scale;
                            laser1.localPosition = Vector3.forward * yScale;
                        }
                    }
                    else
                    {
                        LookAt(player.transform.position);
                    }

                    //Attack AFTER Enemy looks at Player b/c Attack() uses Enemy Transform.right to determine where to point laser
                    if(Time.time - nextTimeAttack > 0)
                    {
                        stats.isAttacking = false; //stats.isAttacking is set back to true in Attack()

                        Attack();
                    }
                }
            }
            else if(Is(Type.ROLLER))
            {
                if((DetectsPlayer() || stats.isAttacking) && !player.stats.isInSafeHouse)
                {
                    //Attack AFTER Enemy looks at Player b/c Attack() uses Enemy Transform.right to determine where to point laser
                    if(Time.time - nextTimeAttack > 0)
                    {
                        //If should rest
                        if(attackRestCount % 2 == 0)
                        {
                            nextTimeAttack = Time.time + settings.attackRestTime;
                            stats.isResting = true;
                            stats.isAttacking = false;
                            
                            components.anim.SetBool("Charge", false);


                            attackRestCount++;
                        }
                        //Else should attack
                        else
                        {
                            stats.isResting = false;

                            stats.isAttacking = false; //stats.isAttacking is set back to true in Attack()

                            Attack(); //nextTimeAttack is set in Attack()


                            attackRestCount++;
                        }
                    }
                    else
                    {
                        if(stats.isResting)
                        {
                            Debug.LogError($"Time spent resting: {nextTimeAttack - Time.time} / {settings.attackRestTime}");

                            LookAt(player.transform.position);
                        }
                        else if(stats.isAttacking)
                        {
                            Debug.LogError($"Time spent attacking: {nextTimeAttack - Time.time} / {settings.attackDuration}");

                            //Charge in attackDirection
                            transform.forward = attackDirection;
                            components.agent.destination = attackPoints.to;


                            if(Physics.CheckBox(transform.TransformPoint(frontBumper.center), frontBumper.extents, transform.rotation, ~(playerLayer | 1 << LayerMask.NameToLayer("Enemy"))))
                            {
                                Debug.LogError("Collided with non-Player");
                                components.anim.Play("HitIntoSomething");
                                stats.isAttacking = false;
                            }
                        }
                    }
                }
                //else
                //{
                //    nextTimeAttack = Time.time + settings.attackRestTime;
                //}
            }
        }
    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if(Is(Type.ROLLER))
    //    {
    //        if(!collision.collider.gameObject.layer.Equals(playerLayer))
    //        {
    //            components.anim.Play("HitIntoSomething");
    //            Debug.LogError($"Hit into {collision.collider.name}");
    //        }
    //        else
    //        {
    //            Debug.LogError("Did not hit Player");
    //        }
    //    }
    //}

    private bool Is(Type type)
    {
        return this.type == type;
    }

    private bool DetectsPlayer()
    {
        Collider c = null;
        try
        {
            c = Physics.OverlapSphere(transform.position, settings.detectionRadius, playerLayer)[0];
        }
        catch(Exception){ }
        //return Physics.OverlapSphere(transform.position, settings.detectionRadius, LayerMask.GetMask("Player"))[0]; //Assumes there is only one Player


        return c;
    }

    private void LookAt(Vector3 position)
    {
        if(Is(Type.FLYINGEYE))
        {
            Vector3 heading = position - transform.GetChild(0).position; //Heading from GFX to Player

            Vector3 transformHeading = heading; //Heading for whole Enemy Transform
            transformHeading.y = 0;
            Vector3 gfxHeading = position - transform.GetChild(0).position; //Heading for Enemy GFX

            transform.forward = transformHeading;
            transform.GetChild(0).forward = gfxHeading;
        }
        else if(Is(Type.ROLLER))
        {
            Vector3 heading = position - transform.position;
            heading.y = 0f;

            transform.forward = heading;
        }
    }

    private void FollowPath()
    {
        components.agent.destination = settings.pathPoints[currentPoint].position;
        Vector3 heading = settings.pathPoints[currentPoint].position - transform.position;
        heading.y = 0;
        sqrDist = heading.sqrMagnitude;

        if(!reachedPoint && sqrDist <= settings.pointReachedBuffer * settings.pointReachedBuffer)
        {
            Debug.LogError("Reached Point " + currentPoint);

            if(settings.restTimes[currentPoint] > 0f)
            {
                if(pathRestCoroutine != null)
                {
                    StopCoroutine(pathRestCoroutine);
                    pathRestCoroutine = null;

                    Debug.LogError("Stopped Coroutine");
                }

                if(pathRestCoroutine == null)
                {
                    pathRestCoroutine = WaitForNextPathPoint();
                    StartCoroutine(pathRestCoroutine);

                    Debug.LogError("Started Coroutine");
                }
            }
            else
            {
                GetNextPathPoint();
            }


            reachedPoint = true;
        }
    }

    private IEnumerator WaitForNextPathPoint()
    {
        yield return new WaitForSeconds(settings.restTimes[currentPoint]);

        GetNextPathPoint();
    }

    private void GetNextPathPoint()
    {
        currentPoint++;

        if(currentPoint >= settings.pathPoints.Length)
            currentPoint = 0;

        pathRestCoroutine = null;
        reachedPoint = false;
    }

    private void SnapToSurface()
    {
        RaycastHit hit;
        if(Physics.Raycast(transform.position, -transform.up, out hit))
            transform.position = new Vector3(transform.position.x, hit.point.y + transform.localScale.y, transform.position.z);
    }

    private void Attack()
    {
        if(!stats.isAttacking)
        {
            //nextTimeAttack = Time.time + settings.attackRestTime;
            nextTimeAttack = Time.time + settings.attackDuration;

            if(Is(Type.FLYINGEYE))
            {
                //Disable laser and look at Player BEFORE calculating from and to so Enemy transform.right can be in correct direction
                laser.gameObject.SetActive(false);
                LookAt(player.transform.position);

                attackPoints.from = player.transform.position + transform.right * 7f;
                attackPoints.current = attackPoints.from;
                attackPoints.to = player.transform.position + transform.right * -7f;

                LookAt(attackPoints.current); //LookAt attackPoint.current BEFORE activating laser or else laser will be activated while Enemy is looking at Player (making the Enemy always hit Player once it starts attacking, which makes it 
                                              //impossible for Player to avoid the attack)

                laser.gameObject.SetActive(true);
            }
            else if(Is(Type.ROLLER))
            {
                attackDirection = (player.transform.position - transform.position).normalized;
                attackDirection.y = 0f;

                transform.forward = attackDirection;

                //Old
                //attackPoints.to = transform.position + attackDirection * 25f;
                //New
                attackPoints.to = player.transform.position + attackDirection * 10f; //Roller destination will be farther than the Player's current position

                components.anim.Play("ChargeStart");
                components.anim.SetBool("Charge", true);
            }

            stats.isAttacking = true;
        }
    }

    public void TakeDamage(float damage)
    {
        if(stats.health > 0)
        {
            stats.health -= damage;
        }

        if(stats.health <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        stats.isDead = true;

        if(Is(Type.FLYINGEYE))
        {
        }
        else
        {
            components.collider.enabled = false;
        }

        components.anim.SetBool("Idle", false);
        components.anim.Play("Die");

        if(Is(Type.FLYINGEYE))
        {
            laser.gameObject.SetActive(false);
            transform.GetChild(0).gameObject.AddComponent<Rigidbody>();
            Destroy(components.collider);
            transform.GetChild(0).gameObject.AddComponent<SphereCollider>().radius = 1.5f; //Current radius of FlyingEye is 1.5
        }

        if(level)
            level.OnEnemyDie(this);



        if(Is(Type.FLYINGEYE))
            Invoke("OnPostDie", 0.5f); //Invoke this after a short delay or else death animation won't play
    }

    private void OnPostDie()
    {
        if(Is(Type.FLYINGEYE))
        {
            transform.GetChild(0).parent = null;
        }


        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, settings.detectionRadius);

        if(Is(Type.FLYINGEYE))
        {

        }
        else if(Is(Type.ROLLER))
        {
            Gizmos.color = Color.green;
            //Gizmos.DrawWireCube(transform.TransformPoint(frontBumper.center), 2f * frontBumper.extents);
            Gizmos.DrawWireMesh(cube, 0, transform.TransformPoint(frontBumper.center), transform.rotation, 2f * frontBumper.extents);

            if(stats.isAttacking)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, transform.position + attackDirection * 25f);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawSphere/*DrawWireSphere*/(attackPoints.to, 1f);
        }
    }
}
