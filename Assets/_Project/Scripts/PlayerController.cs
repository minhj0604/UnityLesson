// ===================================================================================
//  PlayerController.cs
//  - 2D 플랫포머 캐릭터의 "좌우 이동"과 "점프"를 담당하는 스크립트입니다.
//  - Player 오브젝트에 붙여서 사용합니다.
//  - 이 스크립트가 제대로 동작하려면 Player 오브젝트에 아래가 꼭 있어야 합니다.
//      1) Rigidbody2D      : 물리(중력, 속도)를 담당하는 컴포넌트
//      2) Collider2D       : 바닥에 부딪히기 위한 충돌체 (예: CapsuleCollider2D)
//      3) GroundCheck 자식 : 발밑 위치를 알려주는 빈 오브젝트
//  - 그리고 바닥 오브젝트들은 "Ground" 레이어로 설정되어 있어야 하고,
//    Inspector의 Ground Layer 칸에서도 "Ground"를 선택해 줘야 점프가 됩니다.
// ===================================================================================

// using : 다른 곳에 정의된 기능(네임스페이스)을 가져와서 쓰겠다는 선언입니다.
using UnityEngine;              // MonoBehaviour, Rigidbody2D, Vector2 등 Unity의 기본 기능
using UnityEngine.InputSystem;  // 새 Input System (키보드/게임패드 입력)을 쓰기 위한 기능

// public class PlayerController : MonoBehaviour
//  - class        : 설계도(틀)를 만든다는 뜻입니다.
//  - PlayerController : 클래스 이름. 반드시 "파일 이름과 똑같아야" Unity가 인식합니다.
//  - : MonoBehaviour : MonoBehaviour를 상속받는다는 뜻입니다.
//                      상속받아야 오브젝트에 컴포넌트로 붙일 수 있고,
//                      Awake / Update / FixedUpdate 같은 함수를 Unity가 알아서 불러줍니다.
public class PlayerController : MonoBehaviour
{
    // -------------------------------------------------------------------------------
    //  [변수 선언부]
    //  [SerializeField] : private(외부에서 못 건드림) 변수지만
    //                     Inspector 창에는 보이게 해서 값을 바꿀 수 있게 해 줍니다.
    //  [Header("...")]  : Inspector 창에 제목(구분선)을 표시해 줍니다. 보기 좋게 정리용.
    // -------------------------------------------------------------------------------

    [Header("입력")]
    // InputActionReference : Input Actions 파일(InputSystem_Actions)에 만들어 둔
    //                        "행동(Action)"을 가리키는 연결선입니다.
    [SerializeField] private InputActionReference moveAction;  // 이동 입력 (A/D, 방향키, 스틱 등)
    [SerializeField] private InputActionReference jumpAction;  // 점프 입력 (Space, 게임패드 버튼 등)

    [Header("이동")]
    // float : 소수점이 있는 숫자 자료형. 숫자 뒤의 f는 "float 값이에요"라는 표시입니다.
    [SerializeField] private float moveSpeed = 8f;  // 좌우 이동 속도 (1초에 8칸 정도 이동)

    [Header("점프")]
    [SerializeField] private float jumpForce = 14f;            // 점프할 때 위로 주는 속도. 클수록 높이 뜀
    [SerializeField] private float jumpCutMultiplier = 0.5f;   // 일찍 떼면 상승 속도를 이만큼 곱함 (0.5 = 절반으로 줄임)
    [SerializeField] private float coyoteTime = 0.1f;          // 코요테 타임: 발판에서 떨어진 뒤에도 0.1초 동안은 점프 허용
    [SerializeField] private float jumpBufferTime = 0.1f;      // 점프 버퍼: 착지 0.1초 전에 미리 눌러도 착지하자마자 점프

    [Header("바닥 체크")]
    // Transform : 오브젝트의 위치/회전/크기 정보. 여기서는 발밑(GroundCheck)의 위치를 쓰려고 받습니다.
    [SerializeField] private Transform groundCheck;
    // Vector2 : x, y 두 개의 숫자를 묶은 자료형. 여기서는 바닥 검사 상자의 크기(가로 0.8, 세로 0.1)
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.8f, 0.1f);
    // LayerMask : "어떤 레이어를 검사할지" 고르는 값. 여기서 Ground를 선택해야 바닥으로 인식합니다.
    //             (Nothing으로 두면 바닥을 절대 못 찾아서 점프가 안 됩니다!)
    [SerializeField] private LayerMask groundLayer;

    // 아래 변수들은 [SerializeField]가 없으므로 Inspector에 안 보이고, 코드 안에서만 씁니다.
    private Rigidbody2D rb;          // 이 오브젝트의 Rigidbody2D를 담아둘 변수 (Awake에서 채움)
    private float moveInput;         // 현재 좌우 입력값 (-1 = 왼쪽, 0 = 안 누름, 1 = 오른쪽)
    private float coyoteTimer;       // 코요테 타임이 얼마나 남았는지 세는 타이머
    private float jumpBufferTimer;   // 점프 버퍼가 얼마나 남았는지 세는 타이머

    // 프로퍼티(Property) : 변수처럼 쓰지만 읽기/쓰기 권한을 따로 정할 수 있습니다.
    //  - get         : 다른 스크립트에서도 값을 "읽을" 수 있음
    //  - private set : 값을 "바꾸는" 건 이 스크립트 안에서만 가능
    public bool IsGrounded { get; private set; }               // 지금 바닥에 서 있는지 (true/false)
    public bool IsFacingRight { get; private set; } = true;    // 지금 오른쪽을 보고 있는지. 시작은 오른쪽(true)

    // public 변수 : 다른 스크립트에서도 읽고 쓸 수 있고, Inspector에도 보입니다.
    // (현재 이 스크립트 안에서는 사용하지 않는 연습용 변수입니다.)
    public bool isFirst = false;

    // -------------------------------------------------------------------------------
    //  Awake()
    //  - 오브젝트가 생성될 때 "가장 먼저 딱 한 번" Unity가 자동으로 호출하는 함수입니다.
    //  - 보통 GetComponent 같은 "준비 작업"을 여기서 합니다.
    // -------------------------------------------------------------------------------
    private void Awake()
    {
        // GetComponent<Rigidbody2D>() : 같은 오브젝트에 붙어 있는 Rigidbody2D를 찾아서 가져옵니다.
        // 매 프레임 찾으면 느리니까, 한 번 찾아서 rb 변수에 저장해 두고 계속 씁니다.
        rb = GetComponent<Rigidbody2D>();
    }

    // -------------------------------------------------------------------------------
    //  OnEnable()
    //  - 오브젝트(또는 이 컴포넌트)가 "켜질 때마다" 호출되는 함수입니다.
    //  - Input System의 액션은 Enable() 해줘야 입력을 받기 시작합니다.
    // -------------------------------------------------------------------------------
    private void OnEnable()
    {
        moveAction.action.Enable();  // 이동 입력 받기 시작
        jumpAction.action.Enable();  // 점프 입력 받기 시작
    }

    // -------------------------------------------------------------------------------
    //  Update()
    //  - "매 프레임마다" 호출되는 함수입니다. (60FPS면 1초에 약 60번)
    //  - 키 입력 확인은 여기서 해야 놓치지 않습니다.
    //    (FixedUpdate는 프레임과 박자가 달라서 "이번 프레임에 눌렀는지"를 놓칠 수 있음)
    // -------------------------------------------------------------------------------
    private void Update()
    {
        // ReadValue<Vector2>() : 이동 입력을 (x, y) 값으로 읽어옵니다.
        // .x                   : 그중 좌우 값만 사용합니다. (2D 횡스크롤이라 위아래 입력은 안 씀)
        moveInput = moveAction.action.ReadValue<Vector2>().x;

        // ---------------- 바닥 체크 + 코요테 타이머 ----------------
        IsGrounded = CheckGround();            // 발밑에 바닥이 있는지 검사해서 저장
        if (IsGrounded)                        // 만약 바닥에 서 있다면
            coyoteTimer = coyoteTime;          // 바닥에 있는 동안은 계속 가득 채움 (0.1초로 리셋)
        else                                   // 바닥에 없다면 (공중이라면)
            coyoteTimer -= Time.deltaTime;     // 떨어지면 줄어들기 시작
                                               // Time.deltaTime : 지난 프레임부터 지금까지 걸린 시간(초)
                                               // 매 프레임 이만큼 빼면 "실제 시간"만큼 줄어듭니다.

        // ---------------- 점프 버퍼 타이머 ----------------
        // WasPressedThisFrame() : "바로 이번 프레임에" 버튼을 눌렀으면 true (누르고 있는 동안 계속 true가 아님!)
        if (jumpAction.action.WasPressedThisFrame())
        {
            // Debug.Log : Console 창에 글자를 출력합니다. 디버깅(문제 찾기)할 때 아주 유용합니다.
            // $"...{변수}..." : 문자열 안에 변수 값을 끼워 넣는 문법입니다.
            Debug.Log($"Jump pressed / Grounded: {IsGrounded}");
            jumpBufferTimer = jumpBufferTime;  // 누른 순간 기억 (0.1초 동안 "점프하고 싶다"를 기억)
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime; // 안 눌렀으면 기억이 점점 사라짐
        }

        // ---------------- 실제 점프 ----------------
        // 둘 다 남아 있으면 점프
        //  - jumpBufferTimer > 0 : 최근에 점프 버튼을 눌렀다
        //  - coyoteTimer > 0     : 최근까지 바닥에 있었다
        //  - && : "그리고(AND)". 두 조건이 모두 참이어야 실행됩니다.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            Jump();                            // 점프 함수 실행 (아래쪽에 정의되어 있음)
            jumpBufferTimer = 0f;              // 점프했으니 버퍼 비우기 (한 번 누르면 한 번만 점프)
            coyoteTimer = 0f;                  // 공중에서 한 번 더 뛰는 것 방지
        }

        // ---------------- 가변 점프 ----------------
        // 가변 점프: 올라가는 중에 버튼을 떼면 상승 속도 줄이기
        //  → 짧게 누르면 낮게, 길게 누르면 높게 뛰는 효과
        //  - WasReleasedThisFrame() : "바로 이번 프레임에" 버튼에서 손을 뗐으면 true
        //  - rb.linearVelocity.y > 0f : 현재 위로 올라가는 중이면 (y 속도가 양수)
        if (jumpAction.action.WasReleasedThisFrame() && rb.linearVelocity.y > 0f)
        {
            // x 속도는 그대로 두고, y 속도만 jumpCutMultiplier(0.5)를 곱해서 줄입니다.
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }

        // ---------------- 방향 전환 ----------------
        // 방향 전환
        if (moveInput > 0f) Flip(true);        // 오른쪽 입력이면 오른쪽 보기
        else if (moveInput < 0f) Flip(false);  // 왼쪽 입력이면 왼쪽 보기
                                               // 0이면(안 누르면) 아무것도 안 함 → 보던 방향 유지
    }

    // -------------------------------------------------------------------------------
    //  FixedUpdate()
    //  - "일정한 시간 간격"(기본 0.02초 = 1초에 50번)으로 호출되는 함수입니다.
    //  - 물리(Rigidbody) 관련 처리는 여기서 해야 움직임이 일정하고 부드럽습니다.
    // -------------------------------------------------------------------------------
    private void FixedUpdate()
    {
        // linearVelocity : Rigidbody2D의 현재 속도 (x = 좌우, y = 위아래)
        // x 속도 = 입력(-1~1) × 이동속도  → 좌우 이동
        // y 속도 = 원래 y 속도 그대로      → 점프나 중력으로 생긴 위아래 움직임은 건드리지 않음
        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    // -------------------------------------------------------------------------------
    //  OnDrawGizmosSelected()
    //  - Scene 창에서 이 오브젝트를 "선택했을 때만" 보조 그림(Gizmo)을 그려주는 함수입니다.
    //  - 게임 화면에는 안 나오고, 개발할 때 눈으로 확인하는 용도입니다.
    //  - 여기서는 바닥 검사 범위를 초록색 네모로 보여줍니다.
    // -------------------------------------------------------------------------------
    private void OnDrawGizmosSelected()
    {
        // GroundCheck를 연결 안 했으면 그릴 위치가 없으니 그냥 함수 종료
        if (groundCheck == null)
            return;                            // return : 여기서 함수를 끝내고 빠져나감

        Gizmos.color = Color.green;            // 그릴 색을 초록색으로
        // DrawWireCube(중심 위치, 크기) : 속이 빈 상자(테두리만)를 그립니다.
        Gizmos.DrawWireCube(groundCheck.position, groundCheckSize);
    }

    // -------------------------------------------------------------------------------
    //  Jump()
    //  - 실제로 캐릭터를 위로 띄우는 함수입니다.
    //  - private void : 이 스크립트 안에서만 쓰고(private), 돌려주는 값이 없음(void)
    // -------------------------------------------------------------------------------
    private void Jump()
    {
        // AddForce 대신 속도를 직접 넣으면 언제 뛰어도 같은 높이
        //  (AddForce는 떨어지는 중이면 아래 속도와 합쳐져서 점프 높이가 들쭉날쭉해짐)
        // x 속도는 그대로, y 속도만 jumpForce로 "덮어쓰기"
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    // -------------------------------------------------------------------------------
    //  CheckGround()
    //  - 발밑에 바닥이 있는지 검사해서 true(있음) / false(없음)를 돌려주는 함수입니다.
    //  - private bool : 결과로 bool(참/거짓) 값을 돌려줌
    // -------------------------------------------------------------------------------
    private bool CheckGround()
    {
        // GroundCheck를 연결 안 했으면 검사할 수 없으니 "바닥 아님"으로 처리
        if (groundCheck == null)
            return false;

        // Physics2D.OverlapBox(중심, 크기, 회전각도, 검사할 레이어)
        //  - 지정한 위치에 보이지 않는 상자를 놓고, 그 안에 groundLayer에 속한 충돌체가 있는지 찾습니다.
        //  - 찾으면 그 충돌체를, 없으면 null(아무것도 없음)을 돌려줍니다.
        //  - != null : "null이 아니면" → 뭔가 찾았다 → 바닥 위에 있다 → true
        return Physics2D.OverlapBox(groundCheck.position, groundCheckSize, 0f, groundLayer) != null;
    }

    // -------------------------------------------------------------------------------
    //  Flip(bool faceRight)
    //  - 캐릭터가 바라보는 방향을 바꾸는 함수입니다.
    //  - faceRight : 매개변수(함수에 넘겨주는 값). true면 오른쪽, false면 왼쪽을 보게 함
    // -------------------------------------------------------------------------------
    private void Flip(bool faceRight)
    {
        // 이미 그 방향을 보고 있으면 뒤집을 필요가 없으니 함수 종료
        // (이 검사가 없으면 매 프레임 계속 뒤집혀서 캐릭터가 깜빡거림)
        if (faceRight == IsFacingRight)
            return;

        IsFacingRight = faceRight;             // 바라보는 방향 기록 갱신

        // localScale의 x에 -1을 곱하면 좌우가 거울처럼 뒤집힙니다.
        // transform.localScale.x를 직접 바꿀 수는 없어서 (Vector3는 값 복사 방식)
        // 1) 복사해서 꺼내고  2) 값을 바꾸고  3) 다시 넣는 3단계로 처리합니다.
        Vector3 scale = transform.localScale;  // 1) 현재 크기를 복사해서 꺼냄
        scale.x *= -1;                         // 2) x만 -1 곱하기 (1 → -1, -1 → 1)
        transform.localScale = scale;          // 3) 바꾼 값을 다시 넣음
    }
}
