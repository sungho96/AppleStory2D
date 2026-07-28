using System.Collections;
using UnityEngine;

public class PowerShotScreenFeedback : MonoBehaviour
{
    private Camera targetCamera;
    private Texture2D whiteTexture;
    private Texture2D vignetteTexture;
    private Coroutine feedbackRoutine;
    private float originalCameraSize;
    private bool hasStoredCameraSize;
    private float flashAlpha;
    private float vignetteAlpha;

    public void Initialize(Camera cameraReference)
    {
        targetCamera = cameraReference;
        if (whiteTexture == null)
            CreateTextures();
    }

    public void PlayRelease(float power)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        if (whiteTexture == null)
            CreateTextures();

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(ReleaseRoutine(Mathf.Clamp01(power)));
    }

    private IEnumerator ReleaseRoutine(float power)
    {
        float originalSize = targetCamera.orthographicSize;
        originalCameraSize = originalSize;
        hasStoredCameraSize = true;
        float zoomAmount = Mathf.Lerp(0.08f, 0.24f, power);
        float duration = Mathf.Lerp(0.13f, 0.2f, power);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float punch = Mathf.Sin(ratio * Mathf.PI);

            // [파워 샷 화면 후처리] 발사 순간 확대됐다가 빠르게 원래 화면으로 복귀합니다.
            targetCamera.orthographicSize = originalSize - zoomAmount * punch;
            flashAlpha = (1f - ratio) * Mathf.Lerp(0.12f, 0.28f, power);
            vignetteAlpha = punch * Mathf.Lerp(0.16f, 0.34f, power);
            yield return null;
        }

        targetCamera.orthographicSize = originalSize;
        hasStoredCameraSize = false;
        flashAlpha = 0f;
        vignetteAlpha = 0f;
        feedbackRoutine = null;
    }

    private void OnGUI()
    {
        if (flashAlpha <= 0f && vignetteAlpha <= 0f)
            return;

        int previousDepth = GUI.depth;
        Color previousColor = GUI.color;
        GUI.depth = -1000;

        if (flashAlpha > 0f)
        {
            GUI.color = new Color(1f, 0.78f, 0.35f, flashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whiteTexture);
        }

        if (vignetteAlpha > 0f)
        {
            GUI.color = new Color(0.48f, 0.22f, 0.82f, vignetteAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignetteTexture);
        }

        GUI.color = previousColor;
        GUI.depth = previousDepth;
    }

    private void CreateTextures()
    {
        whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        whiteTexture.name = "PowerShotScreenFlashTexture";
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();

        const int size = 64;
        vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        vignetteTexture.name = "PowerShotVignetteTexture";
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2(x, y) / (size - 1f);
                Vector2 centered = (uv - Vector2.one * 0.5f) * 2f;
                float edge = Mathf.Clamp01((centered.magnitude - 0.35f) / 0.65f);
                vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, edge * edge));
            }
        }
        vignetteTexture.Apply();
    }

    private void OnDisable()
    {
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        if (targetCamera != null && hasStoredCameraSize)
            targetCamera.orthographicSize = originalCameraSize;
        hasStoredCameraSize = false;
        flashAlpha = 0f;
        vignetteAlpha = 0f;
        feedbackRoutine = null;
    }

    private void OnDestroy()
    {
        if (whiteTexture != null)
            Destroy(whiteTexture);
        if (vignetteTexture != null)
            Destroy(vignetteTexture);
    }
}
