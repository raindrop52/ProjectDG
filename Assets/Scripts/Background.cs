using UnityEngine;

public class Background : MonoBehaviour
{
    public float moveSpeed = 3f;

    private Rigidbody2D rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        rb.MovePosition(rb.position + Vector2.left * moveSpeed * Time.fixedDeltaTime);
    }
}
