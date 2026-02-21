using UnityEngine;

public class RandomMaterialAssigner : MonoBehaviour
{
    [Header("Target Renderers")]
    [Tooltip("Renderers that will receive a random material.")]
    public Renderer[] targetRenderers;

    [Header("Material Pool")]
    [Tooltip("Possible materials to assign randomly.")]
    public Material[] randomMaterials;

    [Header("Randomize On")]
    public bool randomizeOnStart = true;
    public bool randomizeOnEnable = false;

    private void Start()
    {
        if (randomizeOnStart)
            ApplyRandomMaterial();
    }

    private void OnEnable()
    {
        if (randomizeOnEnable)
            ApplyRandomMaterial();
    }

    [ContextMenu("Apply Random Material")]
    public void ApplyRandomMaterial()
    {
        if (randomMaterials == null || randomMaterials.Length == 0)
        {
            Debug.LogWarning("No materials assigned for randomization.");
            return;
        }

        Material chosen = randomMaterials[Random.Range(0, randomMaterials.Length)];

        foreach (var rend in targetRenderers)
        {
            if (rend != null)
                rend.material = chosen;
        }
    }
}
