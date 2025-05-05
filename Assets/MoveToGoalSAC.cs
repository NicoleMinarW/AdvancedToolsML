using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections;
using System.IO;

public class MoveToGoalSAC : Agent
{
    private Rigidbody2D body;
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    public Transform groundCheck;
    public LayerMask groundLayer;
    public Transform startPosition;
    public string fileName = "temp.csv";

    private string logFilePath;
    private int episodeCounter = 0;
    private float cumulativeReward = 0f;
    private int stepCounter = 0;

    private float sumSteps = 0f;
    private float sumReward = 0f;
    private float sumDistance = 0f;

    private float lastXPosition;
    private float lastCheckpointX;
    private float checkpointSpacing = 5f;

    private float movementCheckX;
    private int movementCheckInterval = 200; // Steps

    public override void Initialize()
    {
        body = GetComponent<Rigidbody2D>();
        logFilePath = Path.Combine(Application.dataPath, fileName);

        if (!File.Exists(logFilePath))
        {
            File.WriteAllText(logFilePath, "Episode,AvgSteps,AvgCumulativeReward,AvgXDistance\n");
        }

        Debug.Log($"Logging training data to: {logFilePath}");
    }

    public override void OnEpisodeBegin()
    {
        if (startPosition != null)
        {
            transform.position = startPosition.position;
            transform.rotation = Quaternion.identity;
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

        episodeCounter++;
        cumulativeReward = 0f;
        stepCounter = 0;
        lastXPosition = transform.position.x;
        lastCheckpointX = transform.position.x;
        movementCheckX = transform.position.x;
    }

    void FixedUpdate()
    {
        if (StepCount > 1000)
        {
            AddReward(-2f);
            cumulativeReward += -2f;
            LogEpisode(false);
            EndEpisode();
        }

        if (StepCount % movementCheckInterval == 0)
        {
            float deltaX = transform.position.x - movementCheckX;
            if (deltaX < 2f)
            {
                AddReward(-1f);
                cumulativeReward += -1f;
            }
            movementCheckX = transform.position.x;
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

        RaycastHit2D hitForward = Physics2D.Raycast(transform.position, Vector2.right, 5f, groundLayer);
        sensor.AddObservation(hitForward.collider != null ? hitForward.distance : 5f);

        Vector2 lowBarDir = (Vector2.right + Vector2.up * 0.5f).normalized;
        RaycastHit2D hitLowBar = Physics2D.Raycast(transform.position, lowBarDir, 5f, groundLayer);
        sensor.AddObservation(hitLowBar.collider != null ? hitLowBar.distance : 5f);

        Debug.DrawRay(transform.position, Vector2.right * 5f, Color.green);
        Debug.DrawRay(transform.position, lowBarDir * 5f, Color.red);

        //if (hitForward.collider != null) Debug.Log("Forward Hit: " + hitForward.collider.name);
        //if (hitLowBar.collider != null) Debug.Log("Low Bar Hit: " + hitLowBar.collider.name);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float move = Mathf.Clamp(actions.ContinuousActions[0], 0f, 1f);
        float jump = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        float squish = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);

        float horizontal = move * moveSpeed;
        body.linearVelocity = new Vector2(horizontal, body.linearVelocity.y);

      
        if (jump > 0.5f && IsGrounded() && transform.localScale.y >= 0.99f)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            
        }

        if (squish > 0.5f)
        {
            
            StartCoroutine(DuckAndUnsquish());
        }

        float xProgress = transform.position.x - lastXPosition;
        AddReward(xProgress * 0.05f);
        cumulativeReward += xProgress * 0.05f;
        lastXPosition = transform.position.x;

        if (transform.position.x > lastCheckpointX + checkpointSpacing)
        {
            AddReward(2f);
            cumulativeReward += 2f;
            lastCheckpointX += checkpointSpacing;
        }

        AddReward(-0.001f);
        cumulativeReward += -0.001f;
        stepCounter++;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous = actionsOut.ContinuousActions;

        continuous[0] = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
        continuous[1] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        continuous[2] = Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
    }

    private bool IsGrounded()
    {
        Collider2D collider = Physics2D.OverlapCircle(groundCheck.position, 0.1f, groundLayer);
        return collider != null;
    }

    private IEnumerator DuckAndUnsquish()
    {
        transform.localScale = new Vector3(1f, 0.5f, 1f);
        yield return new WaitForSeconds(2f);
        transform.localScale = new Vector3(1f, 1f, 1f);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        bool success = false;

        if (collision.CompareTag("Collider") || collision.CompareTag("Bar") || collision.CompareTag("Gap"))
        {
            AddReward(-2f);
            cumulativeReward += -2f;
            LogEpisode(false);
            EndEpisode();
        }
        else if (collision.CompareTag("Checkpoint"))
        {
            AddReward(5f);
            cumulativeReward += 5f;
        }
        else if (collision.CompareTag("Goal"))
        {
            AddReward(10f);
            cumulativeReward += 10f;
            success = true;
            LogEpisode(success);
            EndEpisode();
            Debug.Log("[DEBUG] Reached the goal, ending episode.");
        }
    }

    private void LogEpisode(bool success)
    {
        sumSteps += stepCounter;
        sumReward += cumulativeReward;
        sumDistance += transform.position.x;

        if (episodeCounter % 50 == 0)
        {
            float avgSteps = sumSteps / 50f;
            float avgReward = sumReward / 50f;
            float avgDistance = sumDistance / 50f;

            string line = $"{episodeCounter},{avgSteps:F2},{avgReward:F2},{avgDistance:F2}\n";
            File.AppendAllText(logFilePath, line);

            Debug.Log($"[LOG] Episodes {episodeCounter - 49}-{episodeCounter}: Avg Steps={avgSteps:F2}, Avg Reward={avgReward:F2}, Avg Distance={avgDistance:F2}");

            sumSteps = 0f;
            sumReward = 0f;
            sumDistance = 0f;
        }
    }
}


