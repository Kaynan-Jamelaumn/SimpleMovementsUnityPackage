
using System.Collections.Generic;

[System.Serializable]
public class PortalSettings : BaseSettings
{
    public List<SpawnablePortal> prefabs;
    public int maxNumberOfPortals;
}
