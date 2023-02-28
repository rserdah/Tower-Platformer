using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modified from Brackey's tutorial on YouTube ("FIRST PERSON MOVEMENT in Unity - FPS Controller": https://www.youtube.com/watch?v=_QajrabyTJc)
/// </summary>
public class FPSPlayer : MonoBehaviour
{
    [Serializable]
    public class Controls
    {
        public FPSPlayer player;

        //Tuples won't be in Inspector
        public (string strafe, string frontBack) movementAxes = ("Horizontal", "Vertical");
        public (float strafe, float frontBack) movementInput;

        public (string x, string y) lookAxes = ("Mouse X", "Mouse Y");
        public (float x, float y) lookInput;

        //Can make KeyCode tuple with main KeyCode and alt KeyCode
        public (KeyCode main, KeyCode alt) jump = (KeyCode.Space, KeyCode.None);
        public (KeyCode main, KeyCode alt) run = (KeyCode.LeftShift, KeyCode.RightShift);


        public void Initialize(FPSPlayer player)
        {
            this.player = player;
        }

        public void GetInput()
        {
            movementInput.strafe = Input.GetAxis(movementAxes.strafe); //Maybe also do * Time.deltaTime
            movementInput.frontBack = Input.GetAxis(movementAxes.frontBack); //Maybe also do * Time.deltaTime

            lookInput.x = Input.GetAxis(lookAxes.x) * player.settings.mouseSpeed * Time.deltaTime;
            lookInput.y = Input.GetAxis(lookAxes.y) * player.settings.mouseSpeed * Time.deltaTime;
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
    }

    public class Components
    {
        //public Rigidbody rb;
        public CharacterController characterController;
        public Collider collider;


        public void GetComponents(FPSPlayer player)
        {
            //rb = player.GetComponent<Rigidbody>();
            characterController = player.GetComponent<CharacterController>();
            collider = player.GetComponent<Collider>();
        }
    }

    public Controls controls = new Controls();
    public Settings settings = new Settings();
    public Components components = new Components();

    public new Camera camera;
    public Transform groundCheckOrigin;
    public LayerMask groundLayerMask;

    bool isGrounded;


    //Temp./Holder Variables
    float xRot;
    Vector3 velocityFromGravity = Vector3.zero;


    private void Start()
    {
        controls.Initialize(this);
        components.GetComponents(this);

        //if(components.rb)
        //    components.rb.constraints = RigidbodyConstraints.FreezeRotation;

        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        controls.GetInput();

        Move(controls.movementInput.strafe, controls.movementInput.frontBack);
        Look(controls.lookInput.x, controls.lookInput.y);
    }

    private void Move(float localXInput, float localZInput)
    {
        isGrounded = Physics.CheckSphere(groundCheckOrigin.position, components.characterController.radius, groundLayerMask);

        if(isGrounded && velocityFromGravity.y < 0f)
        {
            velocityFromGravity.y = -2f; //Set it to -2f instead of 0 b/c isGrounded can become true slightly before player actually hits the ground (depending on sphere check radius)
        }

        if(isGrounded && controls.Jump())
        {
            velocityFromGravity.y = GetJumpForce(settings.jumpHeight);
        }

        //components.rb.velocity = GetSpeed() * ((localXInput * transform.right) + (localZInput * transform.forward)) + (controls.Jump() ? settings.jumpForce * transform.up : Vector3.zero);
        components.characterController.Move(GetSpeed() * Time.deltaTime * ((transform.right * localXInput) + transform.forward * localZInput));

        velocityFromGravity.y += settings.gravity * Time.deltaTime;
        components.characterController.Move(velocityFromGravity * Time.deltaTime); //Multiply velocityFromGravity by Time.deltaTime again b/c deltaY = 1/2 * g * t^2
    }

    private void Look(float horizontalInput, float verticalInput)
    {
        xRot -= verticalInput;
        xRot = Mathf.Clamp(xRot, -90f, 90f);

        transform.Rotate(Vector3.up * horizontalInput); //Rotate player around global Y
        camera.transform.localRotation = Quaternion.Euler(xRot, 0f, 0f); //Rotate camera around local X
    }

    private float GetSpeed()
    {
        if(controls.Run())
            return settings.runSpeed;
        else
            return settings.walkSpeed;
    }

    private float GetJumpForce(float desiredJumpHeight)
    {
        //v = sqrt(height * -2 * gravity)

        return Mathf.Sqrt(desiredJumpHeight * -2f * settings.gravity);
    }
}
