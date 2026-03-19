using UnityEngine;

/// <summary>
/// 방향 전용 처리.
/// - Left / Right / Front / Back 자식 오브젝트 캐싱
/// - 방향 상태 변경
/// - 해당 방향 오브젝트만 활성화
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

    /// <summary>
    /// 방향 오브젝트 캐싱 후 초기 적용.
    /// </summary>
    public void Initialize()
    {
        CacheDirectionTransforms();
        ApplyDirection();
    }

    /// <summary>
    /// 수평 입력 기반 좌/우 방향 갱신.
    /// </summary>
    public void SetFacingByHorizontalInput(float moveInput)
    {
        if (moveInput > 0.01f)
            SetDirection(FacingDir.Right);
        else if (moveInput < -0.01f)
            SetDirection(FacingDir.Left);
    }

    /// <summary>
    /// 사다리 등반 등 뒤를 보게 해야 할 때 사용.
    /// </summary>
    public void SetBack()
    {
        SetDirection(FacingDir.Back);
    }

    /// <summary>
    /// 현재 좌우 방향값 반환.
    /// - Left = -1
    /// - 나머지 = 1
    /// </summary>
    public float GetHorizontalFacingDir()
    {
        if (currentDir == FacingDir.Left)
            return -1f;

        return 1f;
    }

    /// <summary>
    /// 방향 상태 변경 후 표시 적용.
    /// </summary>
    private void SetDirection(FacingDir dir)
    {
        if (currentDir == dir)
            return;

        currentDir = dir;
        ApplyDirection();
    }

    /// <summary>
    /// 현재 방향에 해당하는 오브젝트만 활성화.
    /// </summary>
    private void ApplyDirection()
    {
        if (tLeft != null) tLeft.gameObject.SetActive(currentDir == FacingDir.Left);
        if (tRight != null) tRight.gameObject.SetActive(currentDir == FacingDir.Right);
        if (tFront != null) tFront.gameObject.SetActive(currentDir == FacingDir.Front);
        if (tBack != null) tBack.gameObject.SetActive(currentDir == FacingDir.Back);
    }

    /// <summary>
    /// 방향 표시용 자식 Transform 캐싱.
    /// </summary>
    private void CacheDirectionTransforms()
    {
        tLeft = transform.Find("Left");
        tRight = transform.Find("Right");
        tFront = transform.Find("Front");
        tBack = transform.Find("Back");
    }
}