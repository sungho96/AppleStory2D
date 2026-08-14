using System;
using UnityEngine;

public class GameEntryCharacterIntroAnimator : MonoBehaviour
{
    public enum EntrySide
    {
        Left,
        Right
    }

    [Serializable]
    public class CharacterEntry
    {
        [Header("Target")]
        [SerializeField] private string label;
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform image;
        [SerializeField] private EntrySide entrySide;

        [Header("Intro")]
        [SerializeField] private float startDelay;
        [SerializeField] private float introDuration = 0.55f;
        [SerializeField] private float outsideOffset = 2200f;
        [SerializeField] private float overshoot = 42f;

        [Header("Breath")]
        [SerializeField] private float breathScale = 0.025f;
        [SerializeField] private float breathSpeed = 1.4f;
        [SerializeField] private float breathPhase;

        private Vector2 targetPosition;
        private Vector2 startPosition;
        private Vector2 overshootPosition;
        private Vector3 imageBaseScale;
        private float elapsed;
        private bool introComplete;

        public void CaptureInitialState()
        {
            if (root == null)
                return;

            // [Codex GameEntry] Root only moves; the Image child only handles breath scaling.
            targetPosition = root.anchoredPosition;
            float sideSign = entrySide == EntrySide.Left ? -1f : 1f;
            startPosition = targetPosition + new Vector2(sideSign * outsideOffset, 0f);
            overshootPosition = targetPosition - new Vector2(sideSign * overshoot, 0f);

            if (image != null)
                imageBaseScale = image.localScale;

            elapsed = 0f;
            introComplete = false;
            root.anchoredPosition = startPosition;

            if (image != null)
                image.localScale = imageBaseScale;
        }

        public void Tick(float deltaTime, float globalTime)
        {
            if (root == null)
                return;

            elapsed += deltaTime;
            float introTime = elapsed - startDelay;

            if (!introComplete)
            {
                if (introTime <= 0f)
                {
                    root.anchoredPosition = startPosition;
                    return;
                }

                float normalizedTime = introDuration <= 0f ? 1f : Mathf.Clamp01(introTime / introDuration);
                ApplyIntro(normalizedTime);

                if (normalizedTime >= 1f)
                {
                    introComplete = true;
                    root.anchoredPosition = targetPosition;
                }
            }

            ApplyBreath(globalTime);
        }

        private void ApplyIntro(float normalizedTime)
        {
            // [Codex GameEntry] Two-step interpolation: pass the target slightly, then settle back.
            if (normalizedTime < 0.72f)
            {
                float moveT = EaseOutCubic(normalizedTime / 0.72f);
                root.anchoredPosition = Vector2.LerpUnclamped(startPosition, overshootPosition, moveT);
                return;
            }

            float settleT = EaseOutCubic((normalizedTime - 0.72f) / 0.28f);
            root.anchoredPosition = Vector2.LerpUnclamped(overshootPosition, targetPosition, settleT);
        }

        private void ApplyBreath(float globalTime)
        {
            if (image == null)
                return;

            // [Codex GameEntry] Breath repeats on the Image child so it does not fight Root movement.
            float wave = (Mathf.Sin((globalTime + breathPhase) * breathSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = 1f + wave * breathScale;
            image.localScale = imageBaseScale * scale;
        }

        private static float EaseOutCubic(float value)
        {
            value = Mathf.Clamp01(value);
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }
    }

    [Header("Characters")]
    [SerializeField] private CharacterEntry[] characters;

    private float globalTime;

    private void OnEnable()
    {
        globalTime = 0f;

        if (characters == null)
            return;

        for (int i = 0; i < characters.Length; i++)
            characters[i]?.CaptureInitialState();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        globalTime += deltaTime;

        if (characters == null)
            return;

        for (int i = 0; i < characters.Length; i++)
            characters[i]?.Tick(deltaTime, globalTime);
    }
}
