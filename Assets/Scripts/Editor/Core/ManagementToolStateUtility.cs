using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared EditorPrefs persistence helpers used by management tool editor panels.
/// </summary>
public static class ManagementToolStateUtility
{
    #region Methods

    #region Enum State
    /// <summary>
    /// Loads a persisted enum value from EditorPrefs. 
    /// Takes as arguments a key to identify the state, and a fallback enum value to return if loading fails.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="fallbackValue"></param>
    /// <returns> Returns the loaded enum value, or the fallback value if loading fails. </returns>
    public static TEnum LoadEnumValue<TEnum>(string stateKey, TEnum fallbackValue) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return fallbackValue;

        int fallbackIntValue = Convert.ToInt32(fallbackValue, CultureInfo.InvariantCulture);
        int savedValue = EditorPrefs.GetInt(stateKey, fallbackIntValue);

        if (Enum.IsDefined(typeof(TEnum), savedValue) == false)
            return fallbackValue;

        object boxedValue = Enum.ToObject(typeof(TEnum), savedValue);
        return (TEnum)boxedValue;
    }

    /// <summary>
    /// Saves an enum value to EditorPrefs. 
    /// The value is serialized as its integer representation.
    /// Takes as arguments a key to identify the state and the enum value to save.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="value"></param>
    public static void SaveEnumValue<TEnum>(string stateKey, TEnum value) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        int serializedValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        EditorPrefs.SetInt(stateKey, serializedValue);
    }

    /// <summary>
    /// Loads a persisted list of enum values from EditorPrefs.
    /// Takes as an argument a key to identify the state, and returns the loaded list of enum values.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <returns> Returns the loaded list of enum values, or an empty list if loading fails. </returns>
    public static List<TEnum> LoadEnumList<TEnum>(string stateKey) where TEnum : struct, Enum
    {
        List<TEnum> values = new List<TEnum>();

        if (string.IsNullOrWhiteSpace(stateKey))
            return values;

        string rawValue = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(rawValue))
            return values;

        string[] tokens = rawValue.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        HashSet<int> uniqueValues = new HashSet<int>();

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];

            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) == false)
                continue;

            if (Enum.IsDefined(typeof(TEnum), parsedValue) == false)
                continue;

            if (uniqueValues.Contains(parsedValue))
                continue;

            uniqueValues.Add(parsedValue);
            object boxedValue = Enum.ToObject(typeof(TEnum), parsedValue);
            values.Add((TEnum)boxedValue);
        }

        return values;
    }

    /// <summary>
    /// Saves a list of enum values to EditorPrefs.
    /// Takes as arguments a key to identify the state and the list of enum values to save.
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <param name="stateKey"></param>
    /// <param name="values"></param>
    public static void SaveEnumList<TEnum>(string stateKey, IList<TEnum> values) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (values == null || values.Count == 0)
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        StringBuilder builder = new StringBuilder(values.Count * 3);

        for (int index = 0; index < values.Count; index++)
        {
            if (index > 0)
                builder.Append(',');

            int serializedValue = Convert.ToInt32(values[index], CultureInfo.InvariantCulture);
            builder.Append(serializedValue.ToString(CultureInfo.InvariantCulture));
        }

        EditorPrefs.SetString(stateKey, builder.ToString());
    }
    #endregion

    #region Asset State
    /// <summary>
    /// Saves a reference to an asset in EditorPrefs by storing its asset path.
    /// Takes as arguments a key to identify the state and the asset object reference to save.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <param name="assetObject"></param>
    public static void SaveAssetPath(string stateKey, UnityEngine.Object assetObject)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return;

        if (assetObject == null)
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(assetObject);

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            EditorPrefs.DeleteKey(stateKey);
            return;
        }

        EditorPrefs.SetString(stateKey, assetPath);
    }

    /// <summary>
    /// Loads a reference to a GameObject asset from EditorPrefs by reading its asset path and loading the asset.
    /// Takes as an argument a key to identify the state, and returns the loaded GameObject asset.
    /// </summary>
    /// <param name="stateKey"></param>
    /// <returns> Returns the loaded GameObject asset, or null if loading fails. </returns>
    public static GameObject LoadGameObjectAsset(string stateKey)
    {
        if (string.IsNullOrWhiteSpace(stateKey))
            return null;

        string assetPath = EditorPrefs.GetString(stateKey, string.Empty);

        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
    }
    #endregion

    #endregion
}
