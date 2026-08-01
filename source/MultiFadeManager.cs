using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
public static class MultiFadeManager
{
    public class FadeEntry(float start, float target, float duration,
        Func<float, float> smooth, Action onFinish)
    {
        public float start = start, target = target, duration = duration, elapsed = 0;
        public Func<float, float> smooth {get; private set;} = smooth;
        public Coroutine? coroutine {get; private set;}

        public Action onFinish {get; private set;} = onFinish;

        internal void setCoroutine(RainWorldGame game, object targetObject, FieldInfo field, (object, string) key)
        {
            if (coroutine is not null) return;
            coroutine = MultiFadeManagerRunner.Instance.StartCoroutine(FadeCoroutine(game, targetObject, field, this, key));
        }
    }

    private static readonly Dictionary<(object, string), FadeEntry> activeFades = [];

    public static void FadeField(RainWorldGame game, object targetObject, string fieldName, float targetValue, float duration, Func<float, float>? smooth = null, Action? onFinish = null)
    {
        if (smooth is null) smooth = t => t;
        if (onFinish is null) onFinish = () => {};

        FieldInfo field = targetObject.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
        if (field is null) throw new ArgumentException($"Field '{fieldName}' not found on object of type {targetObject.GetType().Name}");

        float startValue = field.GetValue(targetObject) is float val ? val : 0f;

        var key = (targetObject, fieldName);

        StopFade(targetObject, fieldName);

        var entry = new FadeEntry
        (
            startValue,
            targetValue,
            duration,
            smooth,
            onFinish
        );

        entry.setCoroutine(game,targetObject,field,key);

        activeFades[key] = entry;
    }



    private static IEnumerator FadeCoroutine(RainWorldGame game, object targetObject, FieldInfo field, FadeEntry entry, (object, string) key)
    {
        while (entry.elapsed < entry.duration)
        {
            entry.elapsed += Time.deltaTime * game.TimeSpeedFac;
            float t = Mathf.Clamp01(entry.elapsed / entry.duration);
            float value = Mathf.Lerp(entry.start, entry.target, entry.smooth(t));
            field.SetValue(targetObject, value);
            yield return null;
        }

        field.SetValue(targetObject, entry.target);

        entry.onFinish();
        activeFades.Remove(key);
    }


    public static bool StopFade(object targetObject, string fieldName)
    {
        if (activeFades.TryGetValue((targetObject, fieldName), out var entry))
        {
            if (entry.coroutine is not null)
                MultiFadeManagerRunner.Instance.StopCoroutine(entry.coroutine);

            activeFades.Remove((targetObject, fieldName));
            return true;
        } return false;
    }

    public static bool isFading(object targetObject, string fieldName)
        => activeFades.ContainsKey((targetObject, fieldName));

    public static FadeEntry? GetFade(object targetObject, string fieldName)
        => activeFades.TryGetValue((targetObject, fieldName), out var value) ? value : null;


    private class MultiFadeManagerRunner : MonoBehaviour
    {
        private static MultiFadeManagerRunner? _instance;

        public static MultiFadeManagerRunner Instance
        {
            get
            {
                if (_instance is null)
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