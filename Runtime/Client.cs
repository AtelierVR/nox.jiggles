using GatorDragonGames.JigglePhysics;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Jiggles {
    public class Client : IClientModInitializer {
        private GameObject _updaterGO;

        public void OnInitializeClient(IClientModCoreAPI api) {
            _updaterGO = new GameObject($"[{nameof(JigglePhysics)}] Updater");
            _updaterGO.GetOrAddComponent<JiggleUpdateExample>();
            _updaterGO.DontDestroyOnLoad();
        }

        public void OnDisposeClient() {
            if (_updaterGO != null) {
                _updaterGO.Destroy();
                _updaterGO = null;
            }
        }
    }
}
