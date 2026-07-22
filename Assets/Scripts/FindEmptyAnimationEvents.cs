#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class FindEmptyAnimationEvents
{
    [MenuItem("Tools/Animation/Find Empty Events")]
    private static void FindEmptyEvents()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip");
        int found = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
                continue;

            AnimationEvent[] events =
                AnimationUtility.GetAnimationEvents(clip);

            foreach (AnimationEvent evt in events)
            {
                if (!string.IsNullOrWhiteSpace(evt.functionName))
                    continue;

                Debug.LogWarning(
                    $"빈 Animation Event 발견\n클립: {clip.name}\n경로: {path}\n시간: {evt.time}",
                    clip
                );

                found++;
            }
        }

        Debug.Log($"검사 완료: 빈 Animation Event {found}개 발견"); 
    }
}

#endif