using System.Linq;
using GatorDragonGames.JigglePhysics;
using Nox.CCK.Editor;
using Nox.CCK.Jiggles.Avatars;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nox.CCK.Jiggles.Editor {
    /// <summary>
    /// Inspector of the avatar jiggle module. Everything it drives is resolved at load — the rigs are collected
    /// from the avatar and the group is the avatar descriptor id — so there is nothing to serialize: the
    /// inspector shows a read-only summary (group, flag, rigs and colliders) instead.
    /// </summary>
    [CustomEditor(typeof(JiggleModule))]
    public class JiggleModuleEditor : LocalizedInspectorEditor<JiggleModule> {
        protected override string TitleKey => "jiggles.inspector.title";

        private VisualElement _warning;
        private LocalizedLabel _warningLabel;
        private LocalizedLabel _group;
        private LocalizedLabel _flag;
        private LocalizedFoldout _rigsFoldout;
        private VisualElement _rigs;
        private LocalizedFoldout _collidersFoldout;
        private VisualElement _colliders;
        private VisualTreeAsset _rigItem;
        private VisualTreeAsset _colliderItem;

        public override VisualElement CreateInspectorGUI() {
            var root = base.CreateInspectorGUI();
            if (Content == null)
                return root;

            // The GUI is rebuilt on every hierarchy change while the base keeps its root: drop the previous
            // panel so it is never added twice.
            Content.Clear();

            // nox.style.uss and nox.cck.style.uss are imported by the template (see its <Style> tags).
            var uxml = Resources.Load<VisualTreeAsset>("JiggleModuleEditor");
            _rigItem = Resources.Load<VisualTreeAsset>("JiggleModuleEditor.Rig");
            _colliderItem = Resources.Load<VisualTreeAsset>("JiggleModuleEditor.Collider");
            if (uxml == null || _rigItem == null || _colliderItem == null) {
                Content.Add(new Label("The JiggleModuleEditor uxml templates are not found in Resources."));
                return root;
            }

            var ui = uxml.CloneTree();
            Content.Add(ui);

            _group = ui.Q<LocalizedLabel>("label-group");
            _flag = ui.Q<LocalizedLabel>("label-flag");
            _rigsFoldout = ui.Q<LocalizedFoldout>("foldout-rigs");
            _rigs = ui.Q<VisualElement>("list-rigs");
            _collidersFoldout = ui.Q<LocalizedFoldout>("foldout-colliders");
            _colliders = ui.Q<VisualElement>("list-colliders");

            _warning = ui.Q<VisualElement>("warning-rigs");
            _warningLabel = ui.Q<LocalizedLabel>("label-warning");

            // The panels get their icons from the mod loader stylesheet, this project only ships them as
            // resources (see Packages/nox.cck/Resources/icons).
            SetIcon(ui.Q<VisualElement>("icon"), "icons/warning", new Color32(200, 151, 10, 255));

            Refresh();
            return root;
        }

        /// <summary>Sets a nox.cck icon (see <c>Resources/icons</c>) as the background of <paramref name="element"/>.</summary>
        private static void SetIcon(VisualElement element, string resource, Color tint) {
            if (element == null)
                return;

            var texture = Resources.Load<Texture2D>(resource);
            if (texture != null)
                element.style.backgroundImage = new StyleBackground(texture);

            element.style.unityBackgroundImageTintColor = new StyleColor(tint);
        }

        // Nothing is serialized: the module reads the avatar hierarchy, so the panel follows it.
        protected override void OnAttachToPanel(AttachToPanelEvent evt) {
            // Localizes the markup and keeps it in sync with the language changes.
            base.OnAttachToPanel(evt);

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        protected override void OnDetachFromPanel(DetachFromPanelEvent evt) {
            base.OnDetachFromPanel(evt);

            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        // Back on screen: catch up with what was left alone while the inspector was hidden.
        protected override void OnVisible()
            => Refresh();

        // The avatar hierarchy changes constantly while an avatar is loaded: the panel is only rebuilt when it
        // is actually displayed.
        private void OnHierarchyChanged() {
            if (IsVisible)
                Refresh();
        }

        private void Refresh() {
            if (_group == null || !Target)
                return;

            var rigs = Target.GetComponentsInChildren<JiggleRig>(true);
            var colliders = Target.GetComponentsInChildren<Collider>(true);

            // The group is the descriptor id: until the avatar is loaded there is none to show.
            var group = Target.Group;
            _group.LangKey = group != null ? "jiggles.inspector.group" : "jiggles.inspector.group_pending";
            _group.LangArgs = group != null ? new object[] { group } : null;

            _flag.LangArgs = new object[] { Target.IsEnabled };

            // Rigs left on the JigglePhysics component are driven like the others, but the CCK tooling (gizmos)
            // expects the converted component: warn about them instead of listing them as text.
            var unconverted = rigs.Count(rig => rig && rig is not Jiggle);

            _warning.style.display = unconverted > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _warningLabel.LangArgs = new object[] { unconverted };

            _rigsFoldout.LangArgs = new object[] { rigs.Length };
            _rigs.Clear();

            foreach (var rig in rigs) {
                if (!rig)
                    continue;

                _rigs.Add(MakeRigRow(rig));
            }

            if (rigs.Length == 0)
                _rigs.Add(Empty("jiggles.inspector.no_rigs"));

            _collidersFoldout.LangArgs = new object[] { colliders.Length };
            _colliders.Clear();

            foreach (var collider in colliders) {
                if (!collider)
                    continue;

                _colliders.Add(MakeColliderRow(collider));
            }

            if (colliders.Length == 0)
                _colliders.Add(Empty("jiggles.inspector.no_colliders"));

            // The texts count what the hierarchy just gave: re-format them with the arguments above.
            LocalizeTexts();
        }

        /// <summary>Row of <c>JiggleModuleEditor.Rig.uxml</c>: the rig as a read-only reference.</summary>
        private VisualElement MakeRigRow(JiggleRig rig) {
            var row = _rigItem.CloneTree();

            var field = row.Q<ObjectField>("rig-field");
            field.objectType = typeof(JiggleRig);
            field.value = rig;

            return row;
        }

        /// <summary>Row of <c>JiggleModuleEditor.Collider.uxml</c>: the collider as a read-only reference.</summary>
        private VisualElement MakeColliderRow(Collider collider) {
            var row = _colliderItem.CloneTree();

            var field = row.Q<ObjectField>("collider-field");
            field.objectType = typeof(Collider);
            field.value = collider;

            return row;
        }

        /// <summary>Label of a language entry, for the texts the markup cannot carry (the empty lists).</summary>
        private static LocalizedLabel Empty(string key)
            => new() { LangKey = key };
    }
}
