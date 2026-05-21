using System;
using System.Linq;
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
    private float speed;


    public Vector3 movementDirection;
    [SerializeField] private float gravityScale = 9.81f;
    private bool isRuning;

    [Header("Aim info")]
    [SerializeField] private Transform aim;
    [SerializeField] private LayerMask aimlayerMask;
    private Vector3 lookingDirection;

    private float verticalVelocity;

    private Vector2 moveInput;
    private Vector2 aimInput;

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
        AimTowardMouse();
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

    private void AimTowardMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(aimInput);

        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, aimlayerMask))
        {
            lookingDirection = hitInfo.point - transform.position;
            lookingDirection.y = 0f;
            lookingDirection.Normalize();

            transform.forward = lookingDirection;
        }

        aim.position = new Vector3(hitInfo.point.x, transform.position.y + 1, hitInfo.point.z);
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

        controls.Charactor.Aim.performed += context => aimInput = context.ReadValue<Vector2>();
        controls.Charactor.Aim.canceled += context => aimInput = Vector2.zero;

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
