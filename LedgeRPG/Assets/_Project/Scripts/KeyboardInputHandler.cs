using LedgeRPG.Core.Determinism;
using UnityEngine.InputSystem;

namespace Magi.LedgeRPG
{
    /// Pointy-top hex mapping for a QWEASD-cluster + Space + X keyboard.
    ///   W=N, S=S, E=NE, Q=NW, D=SE, A=SW, X=Examine, Space=Rest.
    /// Returns null when nothing was pressed this frame.
    public static class KeyboardInputHandler
    {
        public static RPGActionKind? ReadActionThisFrame()
        {
            var kb = Keyboard.current;
            if (kb == null) return null;

            if (kb.wKey.wasPressedThisFrame) return RPGActionKind.MoveN;
            if (kb.sKey.wasPressedThisFrame) return RPGActionKind.MoveS;
            if (kb.eKey.wasPressedThisFrame) return RPGActionKind.MoveNE;
            if (kb.qKey.wasPressedThisFrame) return RPGActionKind.MoveNW;
            if (kb.dKey.wasPressedThisFrame) return RPGActionKind.MoveSE;
            if (kb.aKey.wasPressedThisFrame) return RPGActionKind.MoveSW;
            if (kb.xKey.wasPressedThisFrame) return RPGActionKind.Examine;
            if (kb.spaceKey.wasPressedThisFrame) return RPGActionKind.Rest;

            return null;
        }

        public static bool ResetPressedThisFrame()
        {
            var kb = Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
        }
    }
}
