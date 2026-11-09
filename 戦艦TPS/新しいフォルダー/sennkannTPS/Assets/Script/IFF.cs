using NUnit.Framework;
using System; 
using UnityEngine;

namespace Script
{

    internal class IFF : MonoBehaviour
    {
        public bool IsFriend = false;

        public void RegistryToManager()
        {
            if (IsFriend)
            {
                if (!ObjectManager.AllyObjects.Contains(this.gameObject))
                {
                    ObjectManager.AllyObjects.Add(this.gameObject);
                }
            }
            else
            {
                if (!ObjectManager.EnemyObjects.Contains(this.gameObject))
                {
                    ObjectManager.EnemyObjects.Add(this.gameObject);
                }
            }
        }

        //裏切りとかいれちゃう？
    }
}
