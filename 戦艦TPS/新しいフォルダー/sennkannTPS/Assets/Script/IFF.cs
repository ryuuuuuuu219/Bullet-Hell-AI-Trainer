
using System; 
using UnityEngine;

namespace Script
{

    internal class IFF : MonoBehaviour
    {
        public bool IsFriend = false;

        private void OnEnable() => RegistryToManager();
        private void OnDisable()
        {
            ObjectManager.AllyObjects.Remove(gameObject);
            ObjectManager.EnemyObjects.Remove(gameObject);
        }

        public void RegistryToManager()
        {
            ObjectManager.AllyObjects.Remove(gameObject);
            ObjectManager.EnemyObjects.Remove(gameObject);
            if (!isActiveAndEnabled) return;
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
