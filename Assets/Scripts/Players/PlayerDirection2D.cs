using UnityEngine;

/// <summary>
/// Handles the visible facing direction objects.
/// - Caches Left / Right / Front / Back child objects.
/// - Keeps the current facing state.
/// - Enables only the object for the current direction.
/// </summary>
public class PlayerDirection2D : MonoBehaviour
{
    public enum FacingDir
    {
        Left,
        Right,
        Back,
        Front
    }

    private Transform tLeft;
    private Transform tRight;
    private Transform tFront;
    private Transform tBack;

    private FacingDir currentDir = FacingDir.Right;

    public FacingDir CurrentDir => currentDir;

    /// <summary>
    /// Cache direction objects and apply the initial state.
    /// </summary>
    public void Initialize()
    {
        CacheDirectionTransforms();
        ApplyDirection();
    }

    /// <summary>
    /// Update left/right facing from horizontal input.
    /// </summary>
    public void SetFacingByHorizontalInput(float moveInput)
    {
        if (moveInput > 0.01f)
            SetDirection(FacingDir.Right);
        else if (moveInput < -0.01f)
            SetDirection(FacingDir.Left);
    }

    /// <summary>
    /// Use when the player should face backward, such as ladder climbing.
    /// </summary>
    public void SetBack()
    {
        SetDirection(FacingDir.Back);
    }

    public void SetDirectionFromNetwork(FacingDir dir)
    {
        // Codex: Apply only the visual direction state that NetworkAnimator does not synchronize.
        SetDirection(dir);
    }

    /// <summary>
    /// Returns the current horizontal facing value.
    /// - Left = -1
    /// - Others = 1
    /// </summary>
    public float GetHorizontalFacingDir()
    {
        if (currentDir == FacingDir.Left)
            return -1f;

        return 1f;
    }

    /// <summary>
    /// Change direction state and refresh visibility.
    /// </summary>
    private void SetDirection(FacingDir dir)
    {
        if (currentDir == dir)
            return;

        currentDir = dir;
        ApplyDirection();
    }

    /// <summary>
    /// Enables only the object that matches the current direction.
    /// </summary>
    private void ApplyDirection()
    {
        if (tLeft != null) tLeft.gameObject.SetActive(currentDir == FacingDir.Left);
        if (tRight != null) tRight.gameObject.SetActive(currentDir == FacingDir.Right);
        if (tFront != null) tFront.gameObject.SetActive(currentDir == FacingDir.Front);
        if (tBack != null) tBack.gameObject.SetActive(currentDir == FacingDir.Back);
    }

    /// <summary>
    /// Cache child transforms used for direction display.
    /// </summary>
    private void CacheDirectionTransforms()
    {
        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");
        tFront = transform.Find("Front");
        tBack = transform.Find("Back");
    }
}
