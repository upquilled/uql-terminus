using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

public static class MultiFadeManager
{
    public class FadeEntry
    {
        public float start, target, duration, elapsed;
        public Func<float, float> smooth;
        public Coroutine coroutine;

        public Action onFinish;
    }

    private static Dictionary<(object, string), FadeEntry> activeFades = new();

    public static void FadeField(object targetObject, string fieldName, float targetValue, float duration, Func<float, float>? smooth = null, Action? onFinish = null)
    {
        if (smooth == null) smooth = t => t;
        if (onFinish == null) onFinish = () => { };

        FieldInfo field = targetObject.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field == null) throw new ArgumentException($"Field '{fieldName}' not found on object of type {targetObject.GetType().Name}");

        float startValue = field.GetValue(targetObject) is float val ? val : 0f;

        var key = (targetObject, fieldName);

        StopFade(targetObject, fieldName);

        var entry = new FadeEntry
        {
            start = startValue,
            target = targetValue,
            duration = duration,
            elapsed = 0f,
            smooth = smooth,
            onFinish = onFinish
        };

        entry.coroutine = MultiFadeManagerRunner.Instance.StartCoroutine(FadeCoroutine(targetObject, field, entry, key));

        activeFades[key] = entry;
    }



    private static IEnumerator FadeCoroutine(object targetObject, FieldInfo field, FadeEntry entry, (object, string) key)
    {
        while (entry.elapsed < entry.duration)
        {
            entry.elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(entry.elapsed / entry.duration);
            float value = Mathf.Lerp(entry.start, entry.target, entry.smooth(t));
            field.SetValue(targetObject, value);
            yield return null;
        }

        field.SetValue(targetObject, entry.target);

        entry.onFinish();
        activeFades.Remove(key);
    }


    public static void StopFade(object targetObject, string fieldName)
    {
        if (activeFades.TryGetValue((targetObject, fieldName), out var entry))
        {
            if (entry.coroutine != null)
            {
                MultiFadeManagerRunner.Instance.StopCoroutine(entry.coroutine);
            }
            activeFades.Remove((targetObject, fieldName));
        }
    }

    public static bool isFading(object targetObject, string fieldName)
    {
        return activeFades.ContainsKey((targetObject, fieldName));
    }

    public static FadeEntry? GetFade(object targetObject, string fieldName)
    {
        return activeFades.GetValueOrDefault((targetObject, fieldName));
    }


    private class MultiFadeManagerRunner : MonoBehaviour
    {
        private static MultiFadeManagerRunner _instance;

        public static MultiFadeManagerRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MultiFadeManager");
                    _instance = go.AddComponent<MultiFadeManagerRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }
    }
}