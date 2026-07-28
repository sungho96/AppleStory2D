using System.Collections;
using UnityEngine;

public class RapidVolleyScreenFeedback : MonoBehaviour
{
    private Camera targetCamera;
    private Texture2D whiteTexture;
    private Coroutine feedbackRoutine;
    private Color shotColor;
    private float flashAlpha;
    private float streakAlpha;
    private float streakProgress;
    private int currentShotIndex;

    public void Initialize(Camera cameraReference)
    {
        targetCamera = cameraReference;
        if (whiteTexture == null)
            CreateTextures();
    }

    public void PlayShot(int shotIndex)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        if (whiteTexture == null)
            CreateTextures();
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        // [래피드 볼리 후처리] 발사 순서에 따라 동일 색상의 플래시와 속도선 강도를 누적합니다.
        shotColor = RapidVolleyVisualFeedback.GetShotColor(shotIndex);
        currentShotIndex = shotIndex;
        feedbackRoutine = StartCoroutine(PlayShotRoutine(shotIndex));
    }

    private IEnumerator PlayShotRoutine(int shotIndex)
    {
        bool finalShot = shotIndex == 2;
        float strength = 1f + shotIndex * 0.3f;
        float duration = finalShot ? 0.14f : 0.09f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            // [래피드 볼리 차별화] 파워샷의 줌·비네트 대신 화면을 가르는 순간 속도선을 사용합니다.
            flashAlpha = (1f - ratio) * (finalShot ? 0.16f : 0.075f * strength);
            streakAlpha = Mathf.Sin(ratio * Mathf.PI) * (finalShot ? 0.78f : 0.5f * strength);
            streakProgress = ratio;
            yield return null;
        }

        ClearFeedback();
        feedbackRoutine = null;
    }

    private void OnGUI()
    {
        if (flashAlpha <= 0f && streakAlpha <= 0f)
            return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.depth = -999;
        GUI.color = new Color(shotColor.r, shotColor.g, shotColor.b, flashAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whiteTexture);

        // [래피드 볼리 차별화] 발사마다 대각선 속도선이 화면을 빠르게 통과합니다.
        int streakCount = currentShotIndex == 2 ? 5 : 3;
        float travelX = Mathf.Lerp(-Screen.width * 0.25f, Screen.width * 0.45f, streakProgress);
        Vector2 pivot = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        GUIUtility.RotateAroundPivot(-12f, pivot);
        for (int i = 0; i < streakCount; i++)
        {
            float y = Screen.height * (0.27f + i * 0.12f) + (i % 2 == 0 ? -18f : 18f);
            float width = Screen.width * (0.52f + i * 0.055f);
            float height = currentShotIndex == 2 ? 8f + i * 2f : 5f + i;
            GUI.color = new Color(
                shotColor.r,
                shotColor.g,
                shotColor.b,
                streakAlpha * (1f - i * 0.1f));
            GUI.DrawTexture(new Rect(travelX - i * 55f, y, width, height), whiteTexture);
        }
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void CreateTextures()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.name = "RapidVolleyScreenFlashTexture";
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

    }

    private void ClearFeedback()
    {
        flashAlpha = 0f;
        streakAlpha = 0f;
        streakProgress = 0f;
    }

    private void OnDisable()
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        ClearFeedback();
        feedbackRoutine = null;
    }

    private void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
    }
}
