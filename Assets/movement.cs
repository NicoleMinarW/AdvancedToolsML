using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;

public class old : Agent
{
    private Rigidbody2D body;
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public Transform groundCheck;
    public LayerMask groundLayer;
    public Transform startPosition;

    public override void Initialize()
    {
        body = GetComponent<Rigidbody2D>();
        Debug.Log("Agent initialized.");
    }

    public override void OnEpisodeBegin()
    {
        if (startPosition != null)
        {
            transform.position = startPosition.position;
            Debug.Log($"Episode started. Agent moved to start position at {startPosition.position}");
        }
        else
        {
            Debug.LogWarning("Start Position not set on agent!");
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        transform.localScale = new Vector3(1f, 1f, 1f);

        Debug.Log($"Start grounded state: {IsGrounded()}");
    }

    void FixedUpdate()
    {
        Debug.Log($"FixedUpdate -> Position: {transform.position}, Grounded: {IsGrounded()}, Velocity: {body.linearVelocity}");
        if (transform.position.y < -10f)
        {
            Debug.Log("Agent fell out of bounds. Ending episode.");
            AddReward(-1f);
            EndEpisode();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector2 position = transform.position;
        Vector2 velocity = body.linearVelocity;
        float grounded = IsGrounded() ? 1f : 0f;

        sensor.AddObservation(position);
        sensor.AddObservation(velocity);
        sensor.AddObservation(grounded);

        Debug.Log($"Observations -> Position: {position}, Velocity: {velocity}, Grounded: {grounded}");
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
       Debug.Log($"OnActionReceived -> Actions: {actions.DiscreteActions}");
        int move = actions.DiscreteActions[0];  // 0: no move, 1: right, 2: left
        int jump = actions.DiscreteActions[1];  // 0: no jump, 1: jump
        int squish = actions.DiscreteActions[2]; // 0: no squish, 1: squish

        float horizontal = 0f;

        if (move == 1)
        {
            horizontal = moveSpeed;
            Debug.Log("Action Received: Move Right");
        }
        else
        {
            Debug.Log("Action Received: No Move");
        }

        body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);

        if (jump == 1 && IsGrounded())
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            Debug.Log("Action Received: Jump");
        }

        if (squish == 1)
        {
            StartCoroutine(DuckAndUnsquish());
            Debug.Log("Action Received: Duck");
        }

        AddReward(0.001f);  // small living reward
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;

        discreteActionsOut[0] = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 :
                                Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 2 : 0;

        discreteActionsOut[1] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        discreteActionsOut[2] = Input.GetKey(KeyCode.DownArrow) ? 1 : 0;

        Debug.Log($"Heuristic -> Move: {discreteActionsOut[0]}, Jump: {discreteActionsOut[1]}, Duck: {discreteActionsOut[2]}");
    }

    private bool IsGrounded()
    {
        Collider2D collider = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
        bool grounded = collider != null;

        Debug.Log($"IsGrounded() -> GroundCheck Position: {groundCheck.position}, Radius: 0.1, Grounded: {grounded}, Hit: {(collider != null ? collider.gameObject.name : "None")}");

        return grounded;
    }


    private IEnumerator DuckAndUnsquish()
    {
        transform.localScale = new Vector3(1f, 0.5f, 1f);
        yield return new WaitForSeconds(1f);
        transform.localScale = new Vector3(1f, 1f, 1f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log($"Triggered by: {collision.gameObject.name}, Tag: {collision.tag}");

        if (collision.CompareTag("Collider"))
        {
            Debug.Log("Collision with obstacle. Ending episode with -1 reward.");
            AddReward(-1f);
            EndEpisode();
        }
        else if (collision.CompareTag("Goal"))
        {
            Debug.Log("Reached goal! Ending episode with +1 reward.");
            AddReward(+1f);
            EndEpisode();
        }
    }
}
