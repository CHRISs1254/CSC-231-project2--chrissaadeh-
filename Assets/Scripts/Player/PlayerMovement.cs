using UnityEngine;
using UnityEngine.InputSystem;

// Handles the player's run and jump.
// Needs a Rigidbody2D and a Collider2D on the same GameObject.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Run")]
    [SerializeField] private float moveSpeed = 8f;        // top running speed (units per second)
    [SerializeField] private float acceleration = 70f;    // how fast we reach top speed
    [SerializeField] private float deceleration = 80f;    // how fast we stop when no key is held

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;           // upward speed when the jump starts
    [SerializeField] private float jumpCutMultiplier = 0.5f;  // releasing jump early cuts upward speed (short hop)
    [SerializeField] private float coyoteTime = 0.1f;         // can still jump this long after walking off a ledge
    [SerializeField] private float jumpBufferTime = 0.1f;     // jump pressed slightly before landing still counts
    [SerializeField] private float fallGravityMultiplier = 1.6f; // fall faster than we rise, feels less floaty
    [SerializeField] private float maxFallSpeed = 20f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;           // empty child object at the player's feet
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    [SerializeField] private LayerMask groundLayer;           // which layers count as ground

    // Other scripts (animation, sounds, signature move) read these.
    public bool IsGrounded { get; private set; }
    public bool IsFacingRight { get; private set; } = true;
    public float MoveInput { get; private set; }
    public Rigidbody2D Body => rb;

    // Set to false to freeze controls (when hurt, dead, paused or during a dash).
    [HideInInspector] public bool canMove = true;

    // Raised so other scripts can play sounds or effects without this script knowing about them.
    public event System.Action Jumped;
    public event System.Action Landed;

    private Rigidbody2D rb;
    private float baseGravity;
    private float coyoteCounter;      // counts down after leaving the ground
    private float jumpBufferCounter;  // counts down after pressing jump
    private bool wasGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        baseGravity = rb.gravityScale;
    }

    private void Update()
    {
        ReadInput();
        CheckGround();
        HandleJump();
        HandleFlip();
    }

    private void FixedUpdate()
    {
        HandleRun();
        HandleGravity();
    }

    private void ReadInput()
    {
        if (!canMove)
        {
            MoveInput = 0f;
            return;
        }

        float x = 0f;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        }
        Gamepad pad = Gamepad.current;
        if (pad != null && Mathf.Abs(pad.leftStick.x.ReadValue()) > 0.2f)
            x = Mathf.Sign(pad.leftStick.x.ReadValue());

        MoveInput = x;

        if (JumpPressedThisFrame())
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;
    }

    private void CheckGround()
    {
        IsGrounded = Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer);

        if (IsGrounded)
            coyoteCounter = coyoteTime;
        else
            coyoteCounter -= Time.deltaTime;

        // Landed this frame: we were in the air last frame and now we're on the ground.
        if (IsGrounded && !wasGrounded)
            Landed?.Invoke();
        wasGrounded = IsGrounded;
    }

    private void HandleJump()
    {
        if (!canMove) return;

        // Jump if jump was pressed recently AND we were on the ground recently.
        if (jumpBufferCounter > 0f && coyoteCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            Jumped?.Invoke();
        }

        // Variable jump height: letting go of jump while still rising cuts the jump short.
        if (JumpReleasedThisFrame() && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
    }

    private void HandleRun()
    {
        if (!canMove) return;

        float targetSpeed = MoveInput * moveSpeed;
        float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
        // Move current speed toward target speed by at most (rate * time) this step.
        float newX = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
        rb.linearVelocity = new Vector2(newX, rb.linearVelocity.y);
    }

    private void HandleGravity()
    {
        // Stronger gravity when falling, normal gravity when rising.
        rb.gravityScale = rb.linearVelocity.y < 0f ? baseGravity * fallGravityMultiplier : baseGravity;

        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    private void HandleFlip()
    {
        if (!canMove) return;

        if (MoveInput > 0f && !IsFacingRight) Flip();
        else if (MoveInput < 0f && IsFacingRight) Flip();
    }

    private void Flip()
    {
        IsFacingRight = !IsFacingRight;
        Vector3 s = transform.localScale;
        s.x *= -1f;
        transform.localScale = s;
    }

    private bool JumpPressedThisFrame()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private bool JumpReleasedThisFrame()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;
        return (kb != null && (kb.spaceKey.wasReleasedThisFrame || kb.wKey.wasReleasedThisFrame || kb.upArrowKey.wasReleasedThisFrame))
            || (pad != null && pad.buttonSouth.wasReleasedThisFrame);
    }

    // Draws the ground check box in the Scene view so you can line it up with the feet.
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }
}
