using System.Collections.Generic;
using UnityEngine;

namespace Merino
{
    [CreateAssetMenu]
    public class MerinoTestData : ScriptableObject
    {
        [SerializeField] internal List<MerinoTreeElement> TreeElements = new List<MerinoTreeElement>();
    }
}
