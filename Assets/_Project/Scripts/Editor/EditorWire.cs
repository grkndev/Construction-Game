using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Sets [SerializeField] private fields from editor scripts without making them public.</summary>
    public static class EditorWire
    {
        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.intValue = value;   // also covers enums
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'");
            prop.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(assetPath));
        }
    }
}
