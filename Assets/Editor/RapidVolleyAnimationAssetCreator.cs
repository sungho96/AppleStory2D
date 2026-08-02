#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class RapidVolleyAnimationAssetCreator
{
    private const string SourceClipPath =
        "Assets/_Project/Player/Common/Animation/Upper/ShotBowU.anim";
    private const string OutputClipPath =
        "Assets/_Project/Player/Common/Animation/Upper/RapidVolleyShot.anim";
    private const string ControllerPath =
        "Assets/_Project/Player/Common/Animation/Controller.controller";
    private const string StateName = "RapidVolley";

    [MenuItem("Tools/AppleStory/Create Rapid Volley Animation")]
    public static void EnsureAnimationAsset()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (source == null || controller == null)
        {
            Debug.LogWarning("[래피드 볼리] ShotBowU 또는 Animator Controller를 찾지 못했습니다.");
            return;
        }

        AnimationClip rapidVolley = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        AnimationClip rebuiltClip = BuildBowShotHeldClip(source);
        if (rapidVolley == null)
        {
            rapidVolley = rebuiltClip;
            AssetDatabase.CreateAsset(rapidVolley, OutputClipPath);
        }
        else
        {
            // [Codex RapidVolley 마지막 복구] 기존 참조는 유지하고 BowShot 기반 타이밍만 갱신합니다.
            EditorUtility.CopySerialized(rebuiltClip, rapidVolley);
            Object.DestroyImmediate(rebuiltClip);
            EditorUtility.SetDirty(rapidVolley);
        }

        EnsureUpperState(controller, rapidVolley);
        AssetDatabase.SaveAssets();
        Debug.Log("[래피드 볼리] BowShot 기반 Upper 애니메이션을 복구했습니다.");
    }

    private static AnimationClip BuildBowShotHeldClip(AnimationClip source)
    {
        AnimationClip output = new AnimationClip
        {
            name = "RapidVolleyShot",
            frameRate = source.frameRate,
            wrapMode = WrapMode.Once
        };

        const float readySourceTime = 0.33333334f;
        const float readyTime = 0.14f;
        const float holdEndTime = 0.52f;
        const float animationEndTime = 0.68f;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
            List<Keyframe> keys = new List<Keyframe>();
            foreach (Keyframe sourceKey in sourceCurve.keys)
            {
                Keyframe key = sourceKey;
                key.time = RemapTime(
                    sourceKey.time, source.length, readySourceTime,
                    readyTime, holdEndTime, animationEndTime);
                keys.Add(key);
            }

            // [Codex RapidVolley 마지막 복구] 기존 BowShot의 당긴 값을 세 발 발사 구간 끝까지 유지합니다.
            float heldValue = sourceCurve.Evaluate(readySourceTime);
            keys.Add(new Keyframe(readyTime, heldValue, 0f, 0f));
            keys.Add(new Keyframe(holdEndTime, heldValue, 0f, 0f));
            keys.Sort((left, right) => left.time.CompareTo(right.time));

            AnimationCurve outputCurve = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = sourceCurve.preWrapMode,
                postWrapMode = sourceCurve.postWrapMode
            };
            AnimationUtility.SetEditorCurve(output, binding, outputCurve);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            ObjectReferenceKeyframe[] sourceKeys =
                AnimationUtility.GetObjectReferenceCurve(source, binding);
            List<ObjectReferenceKeyframe> keys = new List<ObjectReferenceKeyframe>();
            foreach (ObjectReferenceKeyframe sourceKey in sourceKeys)
            {
                keys.Add(new ObjectReferenceKeyframe
                {
                    time = RemapTime(
                        sourceKey.time, source.length, readySourceTime,
                        readyTime, holdEndTime, animationEndTime),
                    value = sourceKey.value
                });
            }

            if (sourceKeys.Length > 0)
            {
                ObjectReferenceKeyframe heldFrame = sourceKeys
                    .Where(key => key.time <= readySourceTime)
                    .OrderByDescending(key => key.time)
                    .FirstOrDefault();
                keys.Add(new ObjectReferenceKeyframe { time = readyTime, value = heldFrame.value });
                keys.Add(new ObjectReferenceKeyframe { time = holdEndTime, value = heldFrame.value });
            }

            keys.Sort((left, right) => left.time.CompareTo(right.time));
            AnimationUtility.SetObjectReferenceCurve(output, binding, keys.ToArray());
        }

        AnimationUtility.SetAnimationEvents(output, new AnimationEvent[0]);
        return output;
    }

    private static float RemapTime(
        float sourceTime,
        float sourceLength,
        float readySourceTime,
        float readyTime,
        float holdEndTime,
        float animationEndTime)
    {
        if (sourceTime <= readySourceTime)
            return sourceTime / readySourceTime * readyTime;

        float releaseRatio = (sourceTime - readySourceTime) /
                             Mathf.Max(0.001f, sourceLength - readySourceTime);
        return Mathf.Lerp(holdEndTime, animationEndTime, releaseRatio);
    }

    private static void EnsureUpperState(AnimatorController controller, AnimationClip clip)
    {
        AnimatorControllerLayer upperLayer =
            controller.layers.FirstOrDefault(layer => layer.name == "Upper");
        if (upperLayer == null)
        {
            Debug.LogWarning("[래피드 볼리] Upper Animator 레이어를 찾지 못했습니다.");
            return;
        }

        AnimatorStateMachine stateMachine = upperLayer.stateMachine;
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == StateName);

        if (state == null)
        {
            state = stateMachine.AddState(StateName);
            state.speed = 1f;
            state.writeDefaultValues = true;

            AnimatorStateTransition exitTransition =
                state.AddTransition(stateMachine.defaultState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 1f;
            exitTransition.duration = 0.04f;
        }

        state.motion = clip;
        EditorUtility.SetDirty(controller);
    }
}
#endif
