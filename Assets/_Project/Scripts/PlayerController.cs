using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("입력")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 8f;

    [Header("점프")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCutMultiplier = 0.5f;   // 일찍 떼면 상승 속도를 이만큼 곱함
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("바닥 체크")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    [SerializeField] private LayerMask groundLayer;

    private Rigidbody2D rb;
    private float moveInput;
    private float coyoteTimer;
    private float jumpBufferTimer;

    public bool IsGrounded { get; private set; }
    public bool IsFacingRight { get; private set; } = true;

    public bool isFirst = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
    }

    private void Update()
    {
        moveInput = moveAction.action.ReadValue<Vector2>().x;

        // 바닥 체크 + 코요테 타이머
        IsGrounded = CheckGround();
        if (IsGrounded)
            coyoteTimer = coyoteTime;          // 바닥에 있는 동안은 계속 가득 채움
        else
            coyoteTimer -= Time.deltaTime;     // 떨어지면 줄어들기 시작

        // 점프 버퍼 타이머
        if (jumpAction.action.WasPressedThisFrame())
        {
            Debug.Log($"Jump pressed / Grounded: {IsGrounded}");
            jumpBufferTimer = jumpBufferTime;  // 누른 순간 기억
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        // 둘 다 남아 있으면 점프
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            Jump();
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;                  // 공중에서 한 번 더 뛰는 것 방지
        }

        // 가변 점프: 올라가는 중에 버튼을 떼면 상승 속도 줄이기
        if (jumpAction.action.WasReleasedThisFrame() && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // 방향 전환
        if (moveInput > 0f) Flip(true);
        else if (moveInput < 0f) Flip(false);
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

    private void Jump()
    {
        // AddForce 대신 속도를 직접 넣으면 언제 뛰어도 같은 높이
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private bool CheckGround()
    {
        if (groundCheck == null)
            return false;

        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer) != null;
    }

    private void Flip(bool faceRight)
    {
        if (faceRight == IsFacingRight) 
            return;

        IsFacingRight = faceRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}