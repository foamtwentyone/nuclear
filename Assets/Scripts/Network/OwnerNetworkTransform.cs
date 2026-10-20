using Unity.Netcode.Components;
using UnityEngine;

namespace Nuclear.Network
{
    /// <summary>
    /// NetworkTransform, которым управляет владелец объекта, а не сервер.
    /// Игрок двигается у себя без задержки, остальные получают его позицию.
    /// </summary>
    [DisallowMultipleComponent]
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
