using System;
using GatorDragonGames.JigglePhysics;
using Nox.Avatars;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using UnityEngine;

namespace Nox.Jiggles {
    public class Client : IClientModInitializer {
        private IClientModCoreAPI _api;
        private GameObject _updaterGO;
        private EventSubscription[] _events;

        public void OnInitializeClient(IClientModCoreAPI api) {
            _api = api;
            _updaterGO = new GameObject($"[{nameof(JigglePhysics)}] Updater");
            _updaterGO.GetOrAddComponent<JiggleUpdateExample>();
            _updaterGO.DontDestroyOnLoad();

            _events = new[] {
                api.EventAPI.Subscribe("avatar_check_request", OnCheckRequest),
            };
        }

        public void OnDisposeClient() {
            if (_events != null) {
                foreach (var e in _events)
                    _api?.EventAPI.Unsubscribe(e);
                _events = null;
            }

            if (_updaterGO != null) {
                _updaterGO.Destroy();
                _updaterGO = null;
            }

            _api = null;
        }

        private static void OnCheckRequest(EventData context) {
            if (!context.TryGet<IAvatarDescriptor>(0, out var descriptor))
                return;

            var valid = JiggleModule.Check(descriptor);
            context.Callback(valid);
        }
    }
}
