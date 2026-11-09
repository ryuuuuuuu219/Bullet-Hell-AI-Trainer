using System;
using UnityEngine;

namespace Script
{
    
    internal class Player : MonoBehaviour
    {
        public double Health = 100.0;

        internal double Hit(double power, double penetration)
        {
            if (!isActiveAndEnabled || Health <= 0) return power;
            return ProjectilePenetration.RemainingPower(power, penetration, 0, Health, out Health);
        }

        private void Start()
        {
            if (!TryGetComponent(out IFF iff)) iff = gameObject.AddComponent<IFF>();
            iff.IsFriend = true;
            iff.RegistryToManager();
        }

        //クリックで射撃したり

        //方向キー上下で加速、左右で旋回

    }
}
