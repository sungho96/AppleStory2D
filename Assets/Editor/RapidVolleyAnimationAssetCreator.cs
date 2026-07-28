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
    private const string TriggerName = "RapidVolley";
    private const string StateName = "RapidVolley";

    static RapidVolleyAnimationAssetCreator()
    {
        // [래피드 볼리 전용 애니메이션] Unity 컴파일 완료 후 안전하게 에셋을 생성합니다.
        EditorApplication.delayCall += EnsureAnimationAsset;
    }

    [MenuItem("Tools/AppleStory/Create Rapid Volley Animation")]
    public static void EnsureAnimationAsset()
    {
        AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (source == null || controller == null)
        {
            Debug.LogWarning("[래피드 볼리] 원본 ShotBowU 또는 Animator Controller를 찾지 못했습니다.");
            return;
        }

        AnimationClip rapidVolley = AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath);
        AnimationClip rebuiltClip = BuildHeldRapidVolleyClip(source);
        if (rapidVolley == null)
        {
            rapidVolley = rebuiltClip;
            AssetDatabase.CreateAsset(rapidVolley, OutputClipPath);
        }
        else
        {
            // [래피드 볼리 수정] 기존 에셋 참조를 유지하면서 새 타이밍으로 갱신합니다.
            EditorUtility.CopySerialized(rebuiltClip, rapidVolley);
            Object.DestroyImmediate(rebuiltClip);
            EditorUtility.SetDirty(rapidVolley);
        }

        EnsureControllerState(controller, rapidVolley);
        AssetDatabase.SaveAssets();
        Debug.Log("[래피드 볼리] 전용 AnimationClip과 Animator State 구성을 확인했습니다.");
    }

    private static AnimationClip BuildRapidVolleyClip(AnimationClip source)
    {
        AnimationClip output = new AnimationClip
        {
            name = "RapidVolleyShot",
            frameRate = source.frameRate,
            wrapMode = WrapMode.Once
        };

        const int shotCount = 3;
        const float timeScale = 0.42f;
        const float segmentDuration = 0.21f;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
            List<Keyframe> keys = new List<Keyframe>();

            for (int shot = 0; shot < shotCount; shot++)
            {
                float offset = shot * segmentDuration;
                foreach (Keyframe sourceKey in sourceCurve.keys)
                {
                    Keyframe key = sourceKey;
                    key.time = offset + sourceKey.time * timeScale;
                    key.inTangent = sourceKey.inTangent / timeScale;
                    key.outTangent = sourceKey.outTangent / timeScale;

                    // 마지막 발은 기존 활 동작을 유지하되 끝 자세를 조금 더 오래 보여줍니다.
                    if (shot == 2 && sourceKey.time >= source.length * 0.65f)
                        key.time += 0.025f;

                    keys.Add(key);
                }
            }

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
            for (int shot = 0; shot < shotCount; shot++)
            {
                float offset = shot * segmentDuration;
                foreach (ObjectReferenceKeyframe sourceKey in sourceKeys)
                {
                    keys.Add(new ObjectReferenceKeyframe
                    {
                        time = offset + sourceKey.time * timeScale,
                        value = sourceKey.value
                    });
                }
            }
            AnimationUtility.SetObjectReferenceCurve(output, binding, keys.ToArray());
        }

        AnimationEvent[] events = new AnimationEvent[shotCount];
        for (int shot = 0; shot < shotCount; shot++)
        {
            events[shot] = new AnimationEvent
            {
                time = shot * segmentDuration + 0.14f,
                functionName = "FireRapidVolleyAnimationEvent",
                intParameter = shot
            };
        }
        AnimationUtility.SetAnimationEvents(output, events);
        return output;
    }

    private static AnimationClip BuildHeldRapidVolleyClip(AnimationClip source)
    {
        AnimationClip output = new AnimationClip
        {
            name = "RapidVolleyShot",
            frameRate = source.frameRate,
            wrapMode = WrapMode.Once
        };

        const float readySourceTime = 0.33333334f;
        const float readyTime = 0.18f;
        const float holdEndTime = 0.62f;
        const float animationEndTime = 0.78f;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, binding);
            List<Keyframe> keys = new List<Keyframe>();
            foreach (Keyframe sourceKey in sourceCurve.keys)
            {
                Keyframe key = sourceKey;
                key.time = RemapRapidVolleyTime(
                    sourceKey.time, source.length, readySourceTime,
                    readyTime, holdEndTime, animationEndTime);
                keys.Add(key);
            }

            // [래피드 볼리 수정] 당긴 팔 자세를 세 번째 발사까지 유지합니다.
            keys.Add(new Keyframe(
                holdEndTime, sourceCurve.Evaluate(readySourceTime), 0f, 0f));
            keys.Sort((left, right) => left.time.CompareTo(right.time));
            AnimationUtility.SetEditorCurve(output, binding, new AnimationCurve(keys.ToArray()));
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
                    time = RemapRapidVolleyTime(
                        sourceKey.time, source.length, readySourceTime,
                        readyTime, holdEndTime, animationEndTime),
                    value = sourceKey.value
                });
            }

            ObjectReferenceKeyframe heldFrame = sourceKeys
                .Where(key => key.time <= readySourceTime)
                .OrderByDescending(key => key.time)
                .FirstOrDefault();
            keys.Add(new ObjectReferenceKeyframe
            {
                time = holdEndTime,
                value = heldFrame.value
            });
            keys.Sort((left, right) => left.time.CompareTo(right.time));
            AnimationUtility.SetObjectReferenceCurve(output, binding, keys.ToArray());
        }

        float[] shotTimes = { 0.25f, 0.43f, 0.61f };
        AnimationEvent[] events = new AnimationEvent[shotTimes.Length];
        for (int shot = 0; shot < shotTimes.Length; shot++)
        {
            events[shot] = new AnimationEvent
            {
                time = shotTimes[shot],
                functionName = "FireRapidVolleyAnimationEvent",
                intParameter = shot
            };
        }
        AnimationUtility.SetAnimationEvents(output, events);
        return output;
    }

    private static float RemapRapidVolleyTime(
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

    private static void EnsureControllerState(
        AnimatorController controller,
        AnimationClip clip)
    {
        if (!controller.parameters.Any(parameter => parameter.name == TriggerName))
            controller.AddParameter(TriggerName, AnimatorControllerParameterType.Trigger);

        // [래피드 볼리 수정] 전신 공격 클립이므로 Lower가 아닌 Complex 레이어에서 재생합니다.
        AnimatorControllerLayer complexLayer = controller.layers
            .FirstOrDefault(layer => layer.name == "Complex");
        if (complexLayer == null)
        {
            Debug.LogWarning("[래피드 볼리] Complex Animator 레이어를 찾지 못했습니다.");
            return;
        }

        // [래피드 볼리 수정] 이전 작업에서 Lower에 생성된 상태를 제거합니다.
        AnimatorControllerLayer lowerLayer = controller.layers
            .FirstOrDefault(layer => layer.name == "Lower");
        if (lowerLayer != null)
        {
            AnimatorState oldState = lowerLayer.stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(candidate => candidate.name == StateName);
            if (oldState != null)
                lowerLayer.stateMachine.RemoveState(oldState);
        }

        AnimatorStateMachine stateMachine = complexLayer.stateMachine;
        AnimatorState state = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(candidate => candidate.name == StateName);

        if (state == null)
        {
            state = stateMachine.AddState(StateName);
            state.motion = clip;
            state.speed = 1f;

            AnimatorStateTransition enterTransition = stateMachine.AddAnyStateTransition(state);
            enterTransition.hasExitTime = false;
            enterTransition.duration = 0.02f;
            enterTransition.canTransitionToSelf = false;
            enterTransition.AddCondition(AnimatorConditionMode.If, 0f, TriggerName);

            AnimatorStateTransition exitTransition =
                state.AddTransition(stateMachine.defaultState);
            exitTransition.hasExitTime = true;
            exitTransition.exitTime = 1f;
            exitTransition.duration = 0.04f;
        }
        else
        {
            state.motion = clip;
        }

        EditorUtility.SetDirty(controller);
    }
}
#endif
