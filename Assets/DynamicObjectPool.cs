using System.Collections.Generic;
using UnityEngine;

public class DynamicObjectPool<TKey>
{
    private Dictionary<TKey, Stack<GameObject>> pools = new();

    public int Count(TKey key)
    {
        try
        {
            return pools[key].Count;
        }
        catch { return 0; }
    }

    public void PreWarm(TKey key, GameObject prefab, int count)
    {
        if (!pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>(count);
            pools[key] = stack;
        }

        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab);
            go.SetActive(false);
            stack.Push(go);
        }
    }

    public GameObject Get(TKey key, GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (pools.TryGetValue(key, out var stack) && stack.Count > 0)
        {
            var obj = stack.Pop();
            obj.transform.SetPositionAndRotation(pos, rot);
            obj.SetActive(true);
            return obj;
        }

        // Pool leer oder noch nicht angelegt -> neu instanziieren
        return Object.Instantiate(prefab, pos, rot);
    }

    public GameObject Get(TKey key, GameObject prefab, Transform parent, bool worldPositionStays = true)
    {
        if (pools.TryGetValue(key, out var stack) && stack.Count > 0)
        {
            var obj = stack.Pop();
            obj.transform.SetParent(parent, worldPositionStays);
            obj.SetActive(true);
            return obj;
        }

        // Pool leer oder noch nicht angelegt -> neu instanziieren
        return Object.Instantiate(prefab, parent);
    }

    public GameObject Get(TKey key, GameObject prefab, RectTransform rect, Transform parent)
    {
        if (pools.TryGetValue(key, out var stack) && stack.Count > 0)
        {
            var obj = stack.Pop();
            obj.transform.SetParent(parent);
            obj.SetActive(true);
            return obj;
        }

        // Pool leer oder noch nicht angelegt -> neu instanziieren
        var newObj = Object.Instantiate(prefab, rect);
        newObj.transform.SetParent(parent, true);
        return newObj;
    }

    public void Release(TKey key, GameObject obj)
    {
        obj.SetActive(false);
        if (!pools.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            pools[key] = stack;
        }
        stack.Push(obj);
    }
}