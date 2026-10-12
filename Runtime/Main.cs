using Nox.Avatars;
using Nox.CCK.Jiggles.Avatars;
using Nox.CCK.Language;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Events;
using Nox.CCK.Mods.Initializers;

namespace Nox.Jiggles.Runtime {
    /// <summary>
    /// Main side of the mod: answers the avatar validation request, so an avatar whose jiggle rig has no root
    /// bone is rejected before it is prepared (see <see cref="JiggleModule.Check"/>).
    /// </summary>
    public class Main : IMainModInitializer {
        private IMainModCoreAPI _api;
        private EventSubscription[] _events;
        private LanguagePack _lang;

        public void OnInitializeMain(IMainModCoreAPI api) {
            _api = api;

            // Texts of the mod, from its Assets folder (see Packages/nox.jiggles/Assets/lang.asset).
            _lang = api.AssetAPI.GetAsset<LanguagePack>("lang.asset");
            LanguageManager.AddPack(_lang);

            _events = new[] {
                api.EventAPI.Subscribe("avatar_check_request", OnCheckRequest),
            };
        }

        public void OnDisposeMain() {
            LanguageManager.RemovePack(_lang);
            _lang = null;

            if (_events != null) {
                foreach (var e in _events)
                    _api?.EventAPI.Unsubscribe(e);
                _events = null;
            }

            _api = null;
        }

        private static void OnCheckRequest(EventData context) {
            if (!context.TryGet<IAvatarDescriptor>(0, out var descriptor))
                return;

            // The module owns its own validation.
            context.Callback(JiggleModule.Check(descriptor));
        }
    }
}
