using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Player), typeof(PlayerAnimations))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 4.5f;
    private PlayerAnimations animations;
    private PlayerActions actions;
    private Player player;
    private Rigidbody2D body;
    private Vector2 direction;
    private void Awake()
    {
        player = GetComponent<Player>();
        actions = new PlayerActions();
        body = GetComponent<Rigidbody2D>();
        animations = GetComponent<PlayerAnimations>();
    }
    private void Update()
    {
        direction = player.Stats == null || player.Stats.Health <= 0
            ? Vector2.zero : actions.Movement.Move.ReadValue<Vector2>().normalized;
        animations.SetMoveBoolTransition(direction != Vector2.zero);
        if (direction != Vector2.zero) animations.SetMoveAnimation(direction);
    }
    private void FixedUpdate() { body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime); }
    private void OnEnable() { if (actions != null) actions.Enable(); }
    private void OnDisable() { direction = Vector2.zero; if (actions != null) actions.Disable(); }
    private void OnDestroy() { if (actions != null) actions.Dispose(); }
}
