using System; 
using UnityEngine;

namespace Script
{
    
    internal class Player : MonoBehaviour
    {
        public double Health = 100.0;

        private void Start()
        {
            IFF iff = this.gameObject.AddComponent<IFF>();
            iff.IsFriend = true;
            iff.RegistryToManager();
        }

        //クリックで射撃したり

        //方向キー上下で加速、左右で旋回

    }
}
