using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Player player;

    private PlayerControls controls;

    private CharacterController characterController;    

    private Animator animator;

    [Header("Movemnet info")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float runSpeed;
    [SerializeField] private float turnSpeed;

    private float speed;
    [SerializeField] private float gravityScale = 9.81f;

    public Vector3 movementDirection;
    private float verticalVelocity;
    public Vector2 moveInput{get; private set;}

    private bool isRuning;
    private void Start()
    {
        player = GetComponent<Player>();

        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        speed = walkSpeed;

        AssignControlInput();
    }

    private void Update()
    {
        ApplyMovement();
        ApplyRotation();
        AnimatorControllers();
    }

    private void AnimatorControllers()
    {
        float xVelocity = Vector3.Dot(movementDirection.normalized, transform.right);
        float zVelocity = Vector3.Dot(movementDirection.normalized, transform.forward);

        animator.SetFloat("xVelocity", xVelocity, .1f, Time.deltaTime);
        animator.SetFloat("zVelocity", zVelocity, .1f, Time.deltaTime);

        bool playAnimationRun = isRuning & movementDirection.magnitude > 0;
        animator.SetBool("isRuning", playAnimationRun);
    }

    private void ApplyRotation()
    {
        Vector3 lookingDirection = player.aim.GetMouseHitInfo().point - transform.position;
        lookingDirection.y = 0f;
        lookingDirection.Normalize();

        Quaternion desiredRotation = Quaternion.LookRotation(lookingDirection);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);
    }
    private void ApplyMovement()
    {
        movementDirection = new Vector3(moveInput.x, 0, moveInput.y);
        ApplyGravity();

        if (movementDirection.magnitude > 0)
        {
            characterController.Move(movementDirection * Time.deltaTime * speed);
        }
    }
    private void ApplyGravity()
    {
        if (characterController.isGrounded == false)
        {
            verticalVelocity -= gravityScale * Time.deltaTime;
            movementDirection.y = verticalVelocity;
        }
        else
            verticalVelocity = -.5f;
    }

    private void AssignControlInput()
    {
        controls = player.controls;

        controls.Charactor.Movement.performed += context => moveInput = context.ReadValue<Vector2>();
        controls.Charactor.Movement.canceled += context => moveInput = Vector2.zero;



        controls.Charactor.Run.performed += context =>
        {
            speed = runSpeed;
            isRuning = true;
        };
        controls.Charactor.Run.canceled += context =>
        {
            speed = walkSpeed;
            isRuning = false;
        };
    }
}
