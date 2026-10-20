using System.Collections.Generic;
using UnityEngine;

namespace NovaMarket.Ambient
{
    /// <summary>
    /// Reusable scene objects, one stack per prefab: a vehicle that leaves the camera's surroundings goes
    /// back to the pool and is reused for the next one that appears, instead of destroying and creating.
    /// </summary>
    public sealed class ViewPool<T> where T : Component
    {
        private readonly Dictionary<GameObject, Stack<T>> free = new Dictionary<GameObject, Stack<T>>();
        private readonly Dictionary<T, GameObject> origin = new Dictionary<T, GameObject>();
        private readonly Transform parent;

        public ViewPool(Transform parent)
        {
            this.parent = parent;
        }

        /// <summary>Objects created so far (in use and free).</summary>
        public int Created { get; private set; }

        public T Get(GameObject prefab)
        {
            if (free.TryGetValue(prefab, out Stack<T> stack) && stack.Count > 0) return stack.Pop();
            GameObject instance = Object.Instantiate(prefab, parent);
            instance.SetActive(false);
            var component = instance.GetComponent<T>();
            origin[component] = prefab;
            Created++;
            return component;
        }

        public void Release(T item)
        {
            if (item == null || !origin.TryGetValue(item, out GameObject prefab)) return;
            if (!free.TryGetValue(prefab, out Stack<T> stack)) free[prefab] = stack = new Stack<T>();
            stack.Push(item);
        }
    }
}
