using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utilidades compartidas por el generador de la escena TEST.
///
/// El cableado de los scripts de producción se hace por <see cref="SerializedObject"/>
/// porque casi todos los campos que hay que enlazar son <c>private [SerializeField]</c>.
/// Es la única forma limpia de hacerlo desde el Editor sin tocar esos scripts.
/// </summary>
internal static class TestSceneBuilderUtil
{
    // ==================================================================
    // CAPAS Y TAGS
    // ==================================================================

    /// <summary>Devuelve el índice de una capa por nombre, o 0 (Default) si no existe.</summary>
    public static int LayerIndex(string layerName)
    {
        int index = LayerMask.NameToLayer(layerName);

        if (index < 0)
        {
            Debug.LogWarning($"[TestSceneBuilder] La capa '{layerName}' no existe en el proyecto. Se usará Default.");
            return 0;
        }

        return index;
    }

    public static void SetTag(GameObject target, string tag)
    {
        if (target == null)
        {
            return;
        }

        try
        {
            target.tag = tag;
        }
        catch (System.Exception)
        {
            Debug.LogWarning($"[TestSceneBuilder] El tag '{tag}' no está definido en el proyecto.", target);
        }
    }

    public static void SetLayer(GameObject target, int layer)
    {
        if (target != null)
        {
            target.layer = layer;
        }
    }

    // ==================================================================
    // CABLEADO POR REFLEXIÓN SERIALIZADA
    // ==================================================================

    /// <summary>
    /// Escribe un campo privado [SerializeField] de cualquier tipo. Avisa con el nombre
    /// exacto del campo si no existe: un builder que falla en silencio es peor que uno
    /// que se rompe, porque genera escenas medio cableadas.
    /// </summary>
    public static void SetValue(Component component, string field, object value)
    {
        if (component == null)
        {
            Debug.LogError($"[TestSceneBuilder] No se puede escribir '{field}': el componente es null.");
            return;
        }

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null)
        {
            Debug.LogError(
                $"[TestSceneBuilder] El campo '{field}' no existe en {component.GetType().Name}. " +
                "Revisa que el script de producción no se haya renombrado.",
                component);
            return;
        }

        switch (property.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                property.objectReferenceValue = value as Object;
                break;

            case SerializedPropertyType.Boolean:
                property.boolValue = (bool)value;
                break;

            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
                property.intValue = System.Convert.ToInt32(value);
                break;

            case SerializedPropertyType.Float:
                property.floatValue = System.Convert.ToSingle(value);
                break;

            case SerializedPropertyType.String:
                property.stringValue = value as string;
                break;

            case SerializedPropertyType.Color:
                property.colorValue = (Color)value;
                break;

            case SerializedPropertyType.Vector3:
                property.vector3Value = (Vector3)value;
                break;

            case SerializedPropertyType.Vector2:
                property.vector2Value = (Vector2)value;
                break;

            case SerializedPropertyType.LayerMask:
                property.intValue = (int)value;
                break;

            default:
                Debug.LogWarning(
                    $"[TestSceneBuilder] Tipo '{property.propertyType}' no soportado para '{field}' en {component.GetType().Name}.",
                    component);
                break;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Escribe un array (o List&lt;T&gt;) de referencias a objetos.</summary>
    public static void SetObjectArray(Component component, string field, Object[] values)
    {
        if (component == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(field);

        if (property == null || !property.isArray)
        {
            Debug.LogError(
                $"[TestSceneBuilder] El campo '{field}' no existe o no es un array en {component.GetType().Name}.",
                component);
            return;
        }

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Registra una entrada de <c>ObjectPoolManager.PoolDefinition</c>. Es un
    /// [Serializable] anidado dentro de una List, así que necesita rutas relativas: es el
    /// único cableado del builder que no encaja en SetValue/SetObjectArray.
    /// </summary>
    public static void SetPoolEntry(ObjectPoolManager manager, GameObject prefab, int prewarmCount, int maxSize)
    {
        if (manager == null || prefab == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(manager);
        SerializedProperty pools = serialized.FindProperty("pools");

        if (pools == null || !pools.isArray)
        {
            Debug.LogError("[TestSceneBuilder] ObjectPoolManager no tiene la lista 'pools'.", manager);
            return;
        }

        int index = pools.arraySize;
        pools.InsertArrayElementAtIndex(index);
        pools.arraySize = index + 1;

        SerializedProperty entry = pools.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("prewarmCount").intValue = prewarmCount;
        entry.FindPropertyRelative("maxSize").intValue = maxSize;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
