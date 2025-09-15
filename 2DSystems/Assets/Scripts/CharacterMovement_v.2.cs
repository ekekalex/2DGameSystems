using System.Collections;
using System.Numerics;
using NUnit.Framework.Constraints;
using Unity.Mathematics;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;


public class CharacterMovement : MonoBehaviour
{   
    private float maxSpeed;
    public float maxSpeedGround = 13f;
    public float maxSpeedAir = 15f;
    private bool isSprinting = false;
    public float sprintMultiplier = 1.5f;
    private float acceleration;
    public float accelMultiplier = 0.9f;
    //1 = starts immediately, 0.5 = slidey, 0 = wont start 
    public float accelMultiplierSprinting = 0.1f;
    public float accelMultiplierFalling = 0.3f;
    private float deceleration;
    public float decelFactor = 0.93f;
    //isnt really slidey until btween 0.9 and 1. so like 0.98 is fairly slidey, 0.94 is very subtle
    public float decelFactorSprinting = 0.98f;
    public int maxJumps = 1;
    private int hasJump = 1;
    //the number of jumps the player has
    public float jumpStrength = 40f;
    //should never be greater than maxVertVelocity
    public float jumpEndFactor = 0.4f;
    //0 = jump will be invariable, 0.5 = jump will be cut off, 1 = jump will slam down
    private float jumpEndStrength;
    //the strength of the downward force when a jump is ended by releasing the button while your up velocity is still positive 
    public float maxVertVelocityMulti = 5f;
    //makes the max vertical velocity a multiple of the base jump strength. should never be less than 1!
    private float maxVertVelocity;
    //should always be greater than jumpStrength
    public float gravity = 0.5f;
    private bool isGrounded = false;
    public int maxDashes = 1;
    private int hasDash = 1;
    public float dashStrength = 2;
    public float dashTime = 0.5f;
    private bool isSuspended = false;
    //thinking a suspendded state where your movement is halted when you take damage or exit a dash, giving you a buffer moment
    public float suspensionTime = 1f; 

    private Rigidbody2D rb;
    private InputAction move;
    private InputAction jump;
    private InputAction sprint;
    private InputAction dash;
    private InputAction suspend;
    private SpriteRenderer sprite;


    private UnityEngine.Vector2 bonusVector;


    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb = GetComponent<Rigidbody2D>();
        sprite = GetComponent<SpriteRenderer>();
        move = InputSystem.actions.FindAction("Move");
        jump = InputSystem.actions.FindAction("Jump");
        sprint = InputSystem.actions.FindAction("Sprint");
        dash = InputSystem.actions.FindAction("Attack");
        suspend = InputSystem.actions.FindAction("Interact");
        maxVertVelocity = jumpStrength * maxVertVelocityMulti;
        jumpEndStrength = jumpStrength * jumpEndFactor;

        Debug.Log(move);
        Debug.Log(jump);
        Debug.Log(sprint);
        Debug.Log(dash);
        Debug.Log(suspend);
    }

    private void Update()
    {
        //MOVEMENT
        //accelerate
        if (isSuspended == false)
        {
            rb.linearVelocityX = Mathf.Clamp(rb.linearVelocityX + (acceleration * accelMultiplier), maxSpeed * -1f, maxSpeed);        //linear velocity X =  UnityEngine.Vector2.x of the controller * multiplier, clamped to within the maximum velocity and minimum of max velocity inverted
        }
        //decelerate
        if (/*isGrounded == true && */move.ReadValue<UnityEngine.Vector2>().x < 0.25f && move.ReadValue<UnityEngine.Vector2>().x > -0.25 && rb.linearVelocityX != 0f) //if isGrounded and no controller input, and you arent stationairy, then slowly decelerate. 0.15 is a buffer so micro stick inputs dont induce slide.
        {
            rb.linearVelocityX = Mathf.Clamp(rb.linearVelocityX * deceleration, maxSpeed * -1f, maxSpeed);
        }
        //variable max speed and acceleration, that changes if you are in air on on the ground, and if sprinting
        if (isGrounded == false)
        {
            maxSpeed = maxSpeedAir * sprintMultiplier;
            acceleration = move.ReadValue<UnityEngine.Vector2>().x * accelMultiplierFalling;
            deceleration = decelFactorSprinting;
        }
        else if (isGrounded == false && isSprinting == true)
        {
            maxSpeed = maxSpeedAir * sprintMultiplier;
            acceleration = move.ReadValue<UnityEngine.Vector2>().x * accelMultiplierSprinting;
            deceleration = decelFactorSprinting;
        }
        else if (isGrounded == true && isSprinting == true)
        {
            maxSpeed = maxSpeedGround * sprintMultiplier;
            acceleration = move.ReadValue<UnityEngine.Vector2>().x * accelMultiplierSprinting;
            deceleration = decelFactorSprinting;
        }
        else
        {
            maxSpeed = maxSpeedGround;
            acceleration = move.ReadValue<UnityEngine.Vector2>().x * accelMultiplier;
            deceleration = decelFactor;
        }


        //JUMP
        if (jump.WasPressedThisFrame() && hasJump > 0 && isSuspended == false)
        {
            rb.linearVelocityY = jumpStrength;
            //rb.linearVelocity += new UnityEngine.Vector2 (move.ReadValue<UnityEngine.Vector2>().x * jumpStrength, move.ReadValue<UnityEngine.Vector2>().y * jumpStrength); 
            // the above variant is intended to boost you when you jump, in input direction. horizontal speed calc would have to change to allow for this though
        }
        if (jump.WasReleasedThisFrame() && rb.linearVelocityY > -10 && isSuspended == false) //when you let go of jump, your upward velocity is immediately cut off. this could instead affect a "jumpVelocityContribution" variable, to a larger velocity calculator, so that jumping while being launched upward doesnt immediately cut your momentum when you let go
        {
            rb.linearVelocityY -= jumpEndStrength;
        }

        //RUN
        if (sprint.IsPressed() && rb.linearVelocityX != 0 && isSuspended == false)
        {
            isSprinting = true;
            sprite.color = Color.green;
        }
        else
        {
            isSprinting = false;
            sprite.color = Color.white;
        }

        //DASH
        if (dash.WasPressedThisFrame() && hasDash > 0 && isSuspended == false)
        {
            StartCoroutine(Dash());
        }

        //FALLING
        if (isGrounded == false && isSuspended == false)
        {
            rb.linearVelocityY = Mathf.Clamp(rb.linearVelocityY - gravity, maxVertVelocity * -1, maxVertVelocity);
        }

        //SUSPENDED
        if (suspend.IsPressed())
        {
            isSuspended = true;
        }
        if (suspend.WasReleasedThisFrame())
        {
            isSuspended = false;
        }
        if (isSuspended == true)
        {
            rb.linearVelocity = new UnityEngine.Vector2(rb.linearVelocityX * deceleration, rb.linearVelocityY * deceleration);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        //if (other.gameObject == GameObject.Find("Tilemap"))
        //{
        isGrounded = true;
        rb.linearVelocityY = 0f;
        if (hasJump < maxJumps)
        {
            hasJump = maxJumps;
        }
        if (hasDash < maxDashes)
        {
            hasDash = maxDashes;
        }
        //}
    }
    void OnTriggerExit2D(Collider2D other)
    {
        //if (other.gameObject == GameObject.Find("Tilemap"))
        //{
        isGrounded = false;
        hasJump -= 1;
        //}
    }

    //DASH
    private IEnumerator Dash()
    {
        if (hasDash > 0)
        {
            Debug.Log("dash coroutine triggered!");
            hasDash -= 1;
            isSuspended = true;
            //do the dash movement (probaly a raycast, but maybe adds a bonus velocity to the end to throw you a bit)
            yield return new WaitForSeconds(dashTime);
            //end dash movement
            isSuspended = false;
        }
    }
}
