#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using UnityEditor;

/// <summary>
/// Provides stable key generation and lookup helpers for numeric serialized properties used by scaling rules.
/// </summary>
public static class PlayerScalingStatKeyUtility
{
    #region Constants
    private static readonly string[] StableStringIdPropertyNames =
    {
        "powerUpId",
        "moduleId",
        "presetId",
        "statName",
        "phaseID"
    };

    private static readonly string[] StableIntegerIdPropertyNames =
    {
        "milestoneLevel"
    };
    #endregion

    #region Methods

    #region Public Methods
    /// <summary>
    /// Builds a normalized stat key for a serialized numeric property.
    /// </summary>
    /// <param name="property">Serialized property to convert into a stable key.</param>
    /// <returns>Normalized key string, or empty when property is invalid.</returns>
    public static string BuildStatKey(SerializedProperty property)
    {
        if (property == null)
            return string.Empty;

        if (property.serializedObject == null)
            return string.Empty;

        return NormalizePropertyPath(property.serializedObject, property.propertyPath);
    }

    /// <summary>
    /// Resolves a serialized property from a previously generated stat key.
    /// </summary>
    /// <param name="serializedObject">Serialized object that owns the target property.</param>
    /// <param name="statKey">Key produced by BuildStatKey.</param>
    /// <param name="property">Resolved property when found.</param>
    /// <returns>True when a numeric property was resolved, otherwise false.</returns>
    public static bool TryFindPropertyByStatKey(SerializedObject serializedObject,
                                                string statKey,
                                                out SerializedProperty property)
    {
        property = null;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        SerializedProperty directProperty = serializedObject.FindProperty(statKey);

        if (directProperty != null && IsNumericProperty(directProperty))
        {
            property = directProperty;
            return true;
        }

        if (TryResolveStablePathToProperty(serializedObject, statKey, out SerializedProperty stableProperty) &&
            stableProperty != null &&
            IsNumericProperty(stableProperty))
        {
            property = stableProperty;
            return true;
        }

        return TryFindPropertyByIteratorScan(serializedObject, statKey, out property);
    }

    /// <summary>
    /// Checks whether a serialized property is a supported numeric type for scaling.
    /// </summary>
    /// <param name="property">Property to inspect.</param>
    /// <returns>True when the property is integer or float, otherwise false.</returns>
    public static bool IsNumericProperty(SerializedProperty property)
    {
        if (property == null)
            return false;

        if (property.propertyType == SerializedPropertyType.Integer)
            return true;

        return property.propertyType == SerializedPropertyType.Float;
    }

    /// <summary>
    /// Converts a raw property path into normalized representation with stable list tokens when possible.
    /// </summary>
    /// <param name="serializedObject">Serialized object used to inspect list element IDs.</param>
    /// <param name="propertyPath">Unity raw property path.</param>
    /// <returns>Normalized path suitable as persistent stat key.</returns>
    public static string NormalizePropertyPath(SerializedObject serializedObject, string propertyPath)
    {
        if (serializedObject == null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(propertyPath))
            return string.Empty;

        string[] segments = propertyPath.Split('.');
        StringBuilder outputBuilder = new StringBuilder(propertyPath.Length + 32);
        StringBuilder currentPathBuilder = new StringBuilder(propertyPath.Length + 32);
        int segmentIndex = 0;

        while (segmentIndex < segments.Length)
        {
            string currentSegment = segments[segmentIndex];

            if (currentSegment == "Array" &&
                segmentIndex + 1 < segments.Length &&
                segments[segmentIndex + 1].StartsWith("data[", StringComparison.Ordinal))
            {
                string dataSegment = segments[segmentIndex + 1];
                AppendSegment(outputBuilder, currentSegment);
                AppendSegment(currentPathBuilder, currentSegment);
                AppendSegment(outputBuilder, dataSegment);
                AppendSegment(currentPathBuilder, dataSegment);

                string elementPath = currentPathBuilder.ToString();
                SerializedProperty arrayElementProperty = serializedObject.FindProperty(elementPath);
                string stableToken = ResolveStableArrayElementToken(arrayElementProperty);

                if (string.IsNullOrWhiteSpace(stableToken) == false)
                {
                    string resolvedDataSegment = BuildStableDataSegmentWithFallbackIndex(dataSegment, stableToken);
                    ReplaceLastArrayToken(outputBuilder, resolvedDataSegment);
                }

                segmentIndex += 2;
                continue;
            }

            AppendSegment(outputBuilder, currentSegment);
            AppendSegment(currentPathBuilder, currentSegment);
            segmentIndex += 1;
        }

        return outputBuilder.ToString();
    }
    #endregion

    #region Helpers
    private static void AppendSegment(StringBuilder builder, string segment)
    {
        if (builder.Length > 0)
            builder.Append('.');

        builder.Append(segment);
    }

    /// <summary>
    /// Scans all serialized properties, including non-visible ones, to resolve stat keys emitted from custom drawers.
    /// </summary>
    /// <param name="serializedObject">Serialized object containing candidate properties.</param>
    /// <param name="statKey">Stable key to resolve.</param>
    /// <param name="property">Resolved property when a match is found.</param>
    /// <returns>True when a matching numeric property is found; otherwise false.</returns>
    private static bool TryFindPropertyByIteratorScan(SerializedObject serializedObject,
                                                      string statKey,
                                                      out SerializedProperty property)
    {
        property = null;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        SerializedProperty iterator = serializedObject.GetIterator();

        if (iterator == null)
            return false;

        bool enterChildren = true;

        while (iterator.Next(enterChildren))
        {
            enterChildren = true;

            if (IsNumericProperty(iterator) == false)
                continue;

            string iteratorKey = BuildStatKey(iterator);

            if (string.Equals(iteratorKey, statKey, StringComparison.OrdinalIgnoreCase) == false)
                continue;

            property = iterator.Copy();
            return true;
        }

        return false;
    }

    private static bool TryResolveStablePathToProperty(SerializedObject serializedObject,
                                                       string statKey,
                                                       out SerializedProperty property)
    {
        property = null;

        if (serializedObject == null)
            return false;

        if (string.IsNullOrWhiteSpace(statKey))
            return false;

        string[] pathSegments = statKey.Split('.');

        if (pathSegments == null || pathSegments.Length == 0)
            return false;

        StringBuilder resolvedPathBuilder = new StringBuilder(statKey.Length);
        int segmentIndex = 0;

        while (segmentIndex < pathSegments.Length)
        {
            string currentSegment = pathSegments[segmentIndex];

            if (string.Equals(currentSegment, "Array", StringComparison.Ordinal) &&
                segmentIndex + 1 < pathSegments.Length &&
                TryResolveArrayDataSegment(pathSegments[segmentIndex + 1],
                                           serializedObject,
                                           resolvedPathBuilder,
                                           out string resolvedArrayDataSegment))
            {
                AppendSegment(resolvedPathBuilder, "Array");
                AppendSegment(resolvedPathBuilder, resolvedArrayDataSegment);
                segmentIndex += 2;
                continue;
            }

            AppendSegment(resolvedPathBuilder, currentSegment);
            segmentIndex += 1;
        }

        string resolvedPath = resolvedPathBuilder.ToString();

        if (string.IsNullOrWhiteSpace(resolvedPath))
            return false;

        property = serializedObject.FindProperty(resolvedPath);
        return property != null;
    }

    private static bool TryResolveArrayDataSegment(string dataSegment,
                                                   SerializedObject serializedObject,
                                                   StringBuilder resolvedPathBuilder,
                                                   out string resolvedArrayDataSegment)
    {
        resolvedArrayDataSegment = string.Empty;

        if (string.IsNullOrWhiteSpace(dataSegment))
            return false;

        if (TryParseNumericArrayDataToken(dataSegment, out int numericIndex))
        {
            resolvedArrayDataSegment = string.Format("data[{0}]", numericIndex);
            return true;
        }

        if (TryParseStableArrayDataToken(dataSegment,
                                         out int fallbackIndex,
                                         out string idPropertyName,
                                         out string idPropertyValue) == false)
            return false;

        string arrayPath = resolvedPathBuilder.ToString();

        if (string.IsNullOrWhiteSpace(arrayPath))
            return false;

        SerializedProperty arrayProperty = serializedObject.FindProperty(arrayPath);

        if (arrayProperty == null || arrayProperty.isArray == false)
            return false;

        int resolvedIndex = FindArrayElementIndexByStableId(arrayProperty, idPropertyName, idPropertyValue);

        if (resolvedIndex >= 0)
        {
            resolvedArrayDataSegment = string.Format("data[{0}]", resolvedIndex);
            return true;
        }

        if (fallbackIndex < 0 || fallbackIndex >= arrayProperty.arraySize)
            return false;

        resolvedArrayDataSegment = string.Format("data[{0}]", fallbackIndex);
        return true;
    }

    private static bool TryParseNumericArrayDataToken(string dataSegment, out int index)
    {
        index = -1;

        if (dataSegment.StartsWith("data[", StringComparison.Ordinal) == false)
            return false;

        if (dataSegment.EndsWith("]", StringComparison.Ordinal) == false)
            return false;

        string indexText = dataSegment.Substring(5, dataSegment.Length - 6);

        if (int.TryParse(indexText, out int parsedIndex) == false)
            return false;

        index = parsedIndex;
        return true;
    }

    private static bool TryParseStableArrayDataToken(string dataSegment,
                                                     out int fallbackIndex,
                                                     out string idPropertyName,
                                                     out string idPropertyValue)
    {
        fallbackIndex = -1;
        idPropertyName = string.Empty;
        idPropertyValue = string.Empty;

        if (dataSegment.StartsWith("data[", StringComparison.Ordinal) == false)
            return false;

        if (dataSegment.EndsWith("]", StringComparison.Ordinal) == false)
            return false;

        string tokenContent = dataSegment.Substring(5, dataSegment.Length - 6);
        string stableToken = tokenContent;
        int fallbackSeparatorIndex = tokenContent.IndexOf('|');

        if (fallbackSeparatorIndex > 0 && fallbackSeparatorIndex < tokenContent.Length - 1)
        {
            string fallbackIndexText = tokenContent.Substring(0, fallbackSeparatorIndex).Trim();
            string stableTokenCandidate = tokenContent.Substring(fallbackSeparatorIndex + 1).Trim();

            if (int.TryParse(fallbackIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFallbackIndex))
                fallbackIndex = parsedFallbackIndex;

            stableToken = stableTokenCandidate;
        }

        int separatorIndex = stableToken.IndexOf(':');

        if (separatorIndex <= 0 || separatorIndex >= stableToken.Length - 1)
            return false;

        idPropertyName = stableToken.Substring(0, separatorIndex).Trim();
        idPropertyValue = stableToken.Substring(separatorIndex + 1).Trim();

        if (string.IsNullOrWhiteSpace(idPropertyName))
            return false;

        if (string.IsNullOrWhiteSpace(idPropertyValue))
            return false;

        return true;
    }

    private static int FindArrayElementIndexByStableId(SerializedProperty arrayProperty,
                                                        string idPropertyName,
                                                        string idPropertyValue)
    {
        if (arrayProperty == null || arrayProperty.isArray == false)
            return -1;

        if (string.IsNullOrWhiteSpace(idPropertyName) || string.IsNullOrWhiteSpace(idPropertyValue))
            return -1;

        for (int elementIndex = 0; elementIndex < arrayProperty.arraySize; elementIndex++)
        {
            SerializedProperty arrayElement = arrayProperty.GetArrayElementAtIndex(elementIndex);

            if (arrayElement == null)
                continue;

            SerializedProperty idProperty = arrayElement.FindPropertyRelative(idPropertyName);

            if (idProperty == null)
                continue;

            if (IsMatchingStableIdProperty(idProperty, idPropertyValue) == false)
                continue;

            return elementIndex;
        }

        return -1;
    }

    private static bool IsMatchingStableIdProperty(SerializedProperty idProperty, string tokenValue)
    {
        if (idProperty == null)
            return false;

        if (string.IsNullOrWhiteSpace(tokenValue))
            return false;

        if (idProperty.propertyType == SerializedPropertyType.String)
        {
            string candidateValue = idProperty.stringValue;

            if (string.IsNullOrWhiteSpace(candidateValue))
                return false;

            return string.Equals(candidateValue.Trim(), tokenValue, StringComparison.OrdinalIgnoreCase);
        }

        if (idProperty.propertyType == SerializedPropertyType.Integer)
        {
            if (int.TryParse(tokenValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInteger) == false)
                return false;

            return idProperty.intValue == parsedInteger;
        }

        return false;
    }

    private static string ResolveStableArrayElementToken(SerializedProperty arrayElementProperty)
    {
        if (arrayElementProperty == null)
            return string.Empty;

        for (int candidateIndex = 0; candidateIndex < StableStringIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableStringIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElementProperty.FindPropertyRelative(candidateName);

            if (candidateProperty == null)
                continue;

            if (candidateProperty.propertyType != SerializedPropertyType.String)
                continue;

            if (string.IsNullOrWhiteSpace(candidateProperty.stringValue))
                continue;

            string sanitizedValue = candidateProperty.stringValue.Trim();
            return string.Format("{0}:{1}", candidateName, sanitizedValue);
        }

        for (int candidateIndex = 0; candidateIndex < StableIntegerIdPropertyNames.Length; candidateIndex++)
        {
            string candidateName = StableIntegerIdPropertyNames[candidateIndex];
            SerializedProperty candidateProperty = arrayElementProperty.FindPropertyRelative(candidateName);

            if (candidateProperty == null)
                continue;

            if (candidateProperty.propertyType != SerializedPropertyType.Integer)
                continue;

            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", candidateName, candidateProperty.intValue);
        }

        return string.Empty;
    }

    /// <summary>
    /// Builds one stable array token that stores both current index and semantic ID.
    /// </summary>
    /// <param name="dataSegment">Original data segment in Unity format (for example data[3]).</param>
    /// <param name="stableToken">Semantic stable token in key:value form.</param>
    /// <returns>Combined segment with index fallback when available.</returns>
    private static string BuildStableDataSegmentWithFallbackIndex(string dataSegment, string stableToken)
    {
        if (string.IsNullOrWhiteSpace(stableToken))
            return dataSegment;

        if (TryParseNumericArrayDataToken(dataSegment, out int indexValue) == false)
            return string.Format("data[{0}]", stableToken);

        return string.Format(CultureInfo.InvariantCulture, "data[{0}|{1}]", indexValue, stableToken);
    }

    private static void ReplaceLastArrayToken(StringBuilder pathBuilder, string replacementToken)
    {
        if (pathBuilder == null)
            return;

        int lastDotIndex = pathBuilder.ToString().LastIndexOf('.');

        if (lastDotIndex < 0)
        {
            pathBuilder.Clear();
            pathBuilder.Append(replacementToken);
            return;
        }

        pathBuilder.Remove(lastDotIndex + 1, pathBuilder.Length - lastDotIndex - 1);
        pathBuilder.Append(replacementToken);
    }
    #endregion

    #endregion
}
#endif
