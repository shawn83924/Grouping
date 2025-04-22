using UnityEngine;

[RequireComponent(typeof(MeshRenderer),typeof(Collider))]
public class GroupingObj : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private Collider collider;
    public MeshRenderer MeshRenderer => meshRenderer;
    public Collider Collider => collider;
    private void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        collider = GetComponent<Collider>();
        meshRenderer.sharedMaterial = GroupingManager.Instance.DefaultMaterial;
        GroupingManager.Instance.Objects.Add(this);
    }
}