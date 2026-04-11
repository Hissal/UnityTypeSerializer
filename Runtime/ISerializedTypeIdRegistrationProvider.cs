using System.Collections.Generic;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Implemented by generated per-assembly providers that contribute SerializedTypeId entries.
    /// </summary>
    public interface ISerializedTypeIdRegistrationProvider {
        void Register(IDictionary<string, string> map);
    }
}

