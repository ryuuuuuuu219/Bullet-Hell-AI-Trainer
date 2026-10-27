using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpeciesIdentity : MonoBehaviour
{
    [SerializeField] private SpeciesDefinition definition;
    public SpeciesDefinition Definition => definition;
    public string SpeciesId => definition == null ? string.Empty : definition.speciesId;
    public void Initialize(SpeciesDefinition species) => definition = species;
}
