using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// インスペクタ上にパラメータの単位を表示するための属性
    /// </summary>
    public class UnitAttribute : PropertyAttribute
    {
        public string Unit { get; }
        public string Description { get; }

        public UnitAttribute(string unit, string description = "")
        {
            Unit = unit;
            Description = description;
        }
    }
}
