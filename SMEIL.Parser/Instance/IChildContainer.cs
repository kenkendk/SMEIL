using System.Collections.Generic;

namespace SMEIL.Parser.Instance
{
    /// <summary>
    /// Interface for an instance that has sub-instances
    /// </summary>
    public interface IChildContainer : IInstance
    {
        /// <summary>
        /// The instances for this item
        /// </summary>
        List<Instance.IInstance> Instances { get; }

    }
}