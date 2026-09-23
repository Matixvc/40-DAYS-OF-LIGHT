using System.Collections;
using UnityEngine;

[RequireComponent(typeof(HealthComponent))]
public class DamageFlash : MonoBehaviour
{
    [Header("Configuración del Color")]
    [ColorUsage(true, true)] // Habilita selector HDR para emisión
    [SerializeField] private Color flashColor = new Color(2f, 2f, 0f, 1f); // Amarillo HDR brillante
    [SerializeField] private float flashDuration = 0.12f;

    private Renderer[] renderers;
    private HealthComponent healthComponent;
    private Coroutine flashCoroutine;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        renderers = GetComponentsInChildren<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (healthComponent != null) healthComponent.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (healthComponent != null) healthComponent.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (!healthComponent.IsDead)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine());
        }
    }

    private IEnumerator FlashRoutine()
    {
        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            rend.GetPropertyBlock(propertyBlock);

            // Forzamos el color base y una emisión fuerte que sobreescribe la textura negra
            propertyBlock.SetColor(BaseColorID, flashColor);
            propertyBlock.SetColor(ColorID, flashColor);
            propertyBlock.SetColor(EmissionColorID, flashColor * 5f); // Emisión multiplicada para destacar

            // Habilitar la keyword de emisión si la usa el shader
            foreach (Material mat in rend.materials)
            {
                mat.EnableKeyword("_EMISSION");
            }

            rend.SetPropertyBlock(propertyBlock);
        }

        yield return new WaitForSeconds(flashDuration);

        foreach (Renderer rend in renderers)
        {
            if (rend == null) continue;
            propertyBlock.Clear();
            rend.SetPropertyBlock(propertyBlock);
        }
    }
}